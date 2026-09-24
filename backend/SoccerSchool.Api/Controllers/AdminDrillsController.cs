using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SoccerSchool.Api.Data;
using SoccerSchool.Api.Domain;
using SoccerSchool.Api.Dtos;
using SoccerSchool.Api.Services;

namespace SoccerSchool.Api.Controllers;

/// <summary>
/// Daily Training drills (bilingual) and their assignments. Admins author drills and can assign
/// them to any player, team, or age group. Coaches (a team's coach card carries their login email —
/// see <see cref="ICoachScopeService"/>) can view active drills and assign them only to their own
/// teams and the players rostered on those teams. Accepts the web cookie and the mobile JWT.
/// </summary>
[ApiController]
[Route("api/admin/drills")]
[Authorize(AuthenticationSchemes = AuthSchemes.CookieOrMobileJwt)]
public class AdminDrillsController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly UserManager<ApplicationUser> _users;
    private readonly ICoachScopeService _scopes;

    public AdminDrillsController(AppDbContext db, UserManager<ApplicationUser> users, ICoachScopeService scopes)
    {
        _db = db;
        _users = users;
        _scopes = scopes;
    }

    // ---- Drills ----

    /// <summary>Admins see every drill (archived on request); coaches see active drills only, with
    /// assignment counts limited to what they can see.</summary>
    [HttpGet]
    public async Task<ActionResult<IEnumerable<AdminDrillDto>>> List([FromQuery] bool includeArchived, CancellationToken ct)
    {
        var scope = await _scopes.GetScopeAsync(User, ct);
        if (!scope.IsStaff) return Forbidden();

        var showArchived = includeArchived && scope.IsAdmin;
        var visible = VisibleAssignments(scope);
        var drills = await _db.Drills
            .AsNoTracking()
            .Where(d => showArchived || d.IsActive)
            .OrderBy(d => d.Category).ThenBy(d => d.TitleEn)
            .Select(d => new { Drill = d, AssignmentCount = visible.Count(a => a.DrillId == d.Id) })
            .ToListAsync(ct);
        return Ok(drills.Select(x => ToDto(x.Drill, x.AssignmentCount)));
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<AdminDrillDto>> Get(int id, CancellationToken ct)
    {
        var scope = await _scopes.GetScopeAsync(User, ct);
        if (!scope.IsStaff) return Forbidden();

        var drill = await _db.Drills.AsNoTracking().FirstOrDefaultAsync(d => d.Id == id && (scope.IsAdmin || d.IsActive), ct);
        if (drill is null) return NotFound();
        var count = await VisibleAssignments(scope).CountAsync(a => a.DrillId == id, ct);
        return Ok(ToDto(drill, count));
    }

    [HttpPost]
    [Authorize(Roles = Roles.Admin)]
    public async Task<ActionResult<AdminDrillDto>> Create([FromBody] SaveDrillRequest req, CancellationToken ct)
    {
        var drill = new Drill();
        var error = Apply(drill, req);
        if (error is not null) return BadRequest(error);
        _db.Drills.Add(drill);
        await _db.SaveChangesAsync(ct);
        return Ok(ToDto(drill, 0));
    }

    [HttpPut("{id:int}")]
    [Authorize(Roles = Roles.Admin)]
    public async Task<ActionResult<AdminDrillDto>> Update(int id, [FromBody] SaveDrillRequest req, CancellationToken ct)
    {
        var drill = await _db.Drills.FirstOrDefaultAsync(d => d.Id == id, ct);
        if (drill is null) return NotFound();
        var error = Apply(drill, req);
        if (error is not null) return BadRequest(error);
        drill.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        return await Get(id, ct);
    }

    /// <summary>Hard delete, only for drills no kid has completed yet. Once there's history, the
    /// admin archives instead (IsActive=false via PUT) so streaks and totals stay intact.</summary>
    [HttpDelete("{id:int}")]
    [Authorize(Roles = Roles.Admin)]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        var drill = await _db.Drills.FirstOrDefaultAsync(d => d.Id == id, ct);
        if (drill is null) return NotFound();
        if (await _db.DrillCompletions.AnyAsync(c => c.DrillId == id, ct))
            return Conflict("Players have already completed this drill. Archive it instead so their progress is kept.");
        _db.Drills.Remove(drill);
        await _db.SaveChangesAsync(ct);
        return NoContent();
    }

    // ---- Assignments ----

    /// <summary>Assignments the caller can see, optionally filtered to one drill. Newest first.</summary>
    [HttpGet("assignments")]
    public async Task<ActionResult<IEnumerable<AdminDrillAssignmentDto>>> ListAssignments([FromQuery] int? drillId, CancellationToken ct)
    {
        var scope = await _scopes.GetScopeAsync(User, ct);
        if (!scope.IsStaff) return Forbidden();
        return Ok(await LoadAssignmentsAsync(scope, drillId, assignmentId: null, ct));
    }

    [HttpPost("assignments")]
    public async Task<ActionResult<AdminDrillAssignmentDto>> CreateAssignment([FromBody] CreateDrillAssignmentRequest req, CancellationToken ct)
    {
        var scope = await _scopes.GetScopeAsync(User, ct);
        if (!scope.IsStaff) return Forbidden();

        if (!TrainingWire.TryParseTargetType(req.TargetType, out var type))
            return BadRequest("Target type must be player, team or age-group.");
        if (req.EndDate is not null && req.EndDate < req.StartDate)
            return BadRequest("End date can't be before the start date.");
        if (!await _db.Drills.AnyAsync(d => d.Id == req.DrillId && (scope.IsAdmin || d.IsActive), ct))
            return BadRequest("Drill not found.");

        var targetExists = type switch
        {
            DrillTargetType.Player => await _db.Players.AnyAsync(p => p.Id == req.TargetId, ct),
            DrillTargetType.Team => await _db.Teams.AnyAsync(t => t.Id == req.TargetId, ct),
            _ => await _db.AgeClassifications.AnyAsync(c => c.Id == req.TargetId, ct),
        };
        if (!targetExists) return BadRequest("The selected player, team or age group no longer exists.");
        if (!await CanTargetAsync(scope, type, req.TargetId, ct)) return Forbidden();

        var assignment = new DrillAssignment
        {
            DrillId = req.DrillId,
            TargetType = type,
            PlayerId = type == DrillTargetType.Player ? req.TargetId : null,
            TeamId = type == DrillTargetType.Team ? req.TargetId : null,
            AgeClassificationId = type == DrillTargetType.AgeClassification ? req.TargetId : null,
            StartDate = req.StartDate,
            EndDate = req.EndDate,
            CreatedByUserId = _users.GetUserId(User),
        };
        _db.DrillAssignments.Add(assignment);
        await _db.SaveChangesAsync(ct);

        return Ok((await LoadAssignmentsAsync(scope, drillId: null, assignment.Id, ct)).Single());
    }

    [HttpDelete("assignments/{id:int}")]
    public async Task<IActionResult> DeleteAssignment(int id, CancellationToken ct)
    {
        var scope = await _scopes.GetScopeAsync(User, ct);
        if (!scope.IsStaff) return Forbidden();

        // Coaches only ever see (and so can only remove) assignments inside their scope; anything
        // else is a 404 to them, same as a missing row.
        var assignment = await VisibleAssignments(scope).FirstOrDefaultAsync(a => a.Id == id, ct);
        if (assignment is null) return NotFound();
        _db.DrillAssignments.Remove(assignment);
        await _db.SaveChangesAsync(ct);
        return NoContent();
    }

    /// <summary>Picker options for the assignment form. Admins get every team and age group and
    /// search players through the admin players endpoint; coaches get their own teams plus every
    /// player rostered on them (a short list, so it's returned in full).</summary>
    [HttpGet("targets")]
    public async Task<ActionResult<DrillTargetOptionsDto>> Targets(CancellationToken ct)
    {
        var scope = await _scopes.GetScopeAsync(User, ct);
        if (!scope.IsStaff) return Forbidden();

        var teamIds = scope.CoachTeamIds.ToList();
        var teams = await _db.Teams.AsNoTracking()
            .Where(t => scope.IsAdmin || teamIds.Contains(t.Id))
            .OrderBy(t => t.Name)
            .Select(t => new DrillTargetOptionDto(t.Id, t.Name)).ToListAsync(ct);

        if (scope.IsAdmin)
        {
            var ages = await _db.AgeClassifications.AsNoTracking().OrderBy(c => c.Name)
                .Select(c => new DrillTargetOptionDto(c.Id, c.Name)).ToListAsync(ct);
            return Ok(new DrillTargetOptionsDto(true, teams, ages, Array.Empty<DrillTargetPlayerDto>()));
        }

        var players = await _db.TeamPlayers.AsNoTracking()
            .Where(tp => teamIds.Contains(tp.TeamId))
            .OrderBy(tp => tp.Player!.FirstName).ThenBy(tp => tp.Player!.LastName)
            .Select(tp => new DrillTargetPlayerDto(tp.PlayerId, tp.Player!.FirstName + " " + tp.Player.LastName, tp.Team!.Name))
            .ToListAsync(ct);
        return Ok(new DrillTargetOptionsDto(false, teams, Array.Empty<DrillTargetOptionDto>(), players));
    }

    // ---------------------------------------------------------------------------------------

    private ObjectResult Forbidden() =>
        StatusCode(StatusCodes.Status403Forbidden, "Only admins and team coaches can manage Daily Training drills.");

    /// <summary>Assignments within the caller's scope: everything for admins; for coaches, those
    /// targeting one of their teams or a player rostered on one of their teams.</summary>
    private IQueryable<DrillAssignment> VisibleAssignments(StaffScope scope)
    {
        if (scope.IsAdmin) return _db.DrillAssignments;
        var teamIds = scope.CoachTeamIds.ToList();
        return _db.DrillAssignments.Where(a =>
            (a.TargetType == DrillTargetType.Team && a.TeamId != null && teamIds.Contains(a.TeamId.Value))
            || (a.TargetType == DrillTargetType.Player
                && _db.TeamPlayers.Any(tp => tp.PlayerId == a.PlayerId && teamIds.Contains(tp.TeamId))));
    }

    private async Task<bool> CanTargetAsync(StaffScope scope, DrillTargetType type, int targetId, CancellationToken ct)
    {
        if (scope.IsAdmin) return true;
        var teamIds = scope.CoachTeamIds.ToList();
        return type switch
        {
            DrillTargetType.Team => teamIds.Contains(targetId),
            DrillTargetType.Player => await _db.TeamPlayers.AnyAsync(tp => tp.PlayerId == targetId && teamIds.Contains(tp.TeamId), ct),
            // Age groups span every team, so only admins can target them.
            _ => false,
        };
    }

    private async Task<List<AdminDrillAssignmentDto>> LoadAssignmentsAsync(StaffScope scope, int? drillId, int? assignmentId, CancellationToken ct)
    {
        var rows = await VisibleAssignments(scope)
            .AsNoTracking()
            .Where(a => (drillId == null || a.DrillId == drillId) && (assignmentId == null || a.Id == assignmentId))
            .OrderByDescending(a => a.StartDate).ThenByDescending(a => a.Id)
            .Select(a => new
            {
                a.Id,
                a.DrillId,
                DrillTitle = a.Drill!.TitleEn,
                a.TargetType,
                a.PlayerId,
                a.TeamId,
                a.AgeClassificationId,
                PlayerName = a.Player != null ? a.Player.FirstName + " " + a.Player.LastName : null,
                TeamName = a.Team != null ? a.Team.Name : null,
                AgeGroupName = a.AgeClassification != null ? a.AgeClassification.Name : null,
                a.StartDate,
                a.EndDate,
                a.CreatedAt,
            })
            .ToListAsync(ct);

        return rows.Select(r => new AdminDrillAssignmentDto(
            r.Id,
            r.DrillId,
            r.DrillTitle,
            r.TargetType.ToWire(),
            r.PlayerId ?? r.TeamId ?? r.AgeClassificationId ?? 0,
            r.PlayerName ?? r.TeamName ?? r.AgeGroupName ?? "",
            r.StartDate,
            r.EndDate,
            r.CreatedAt)).ToList();
    }

    /// <summary>Validates and copies the request onto the entity. Returns an error message or null.</summary>
    private static string? Apply(Drill drill, SaveDrillRequest req)
    {
        if (!TrainingWire.TryParseCategory(req.Category, out var category)) return "Pick a category.";
        var titleEn = req.TitleEn?.Trim() ?? "";
        var titleEs = req.TitleEs?.Trim() ?? "";
        if (titleEn.Length == 0 || titleEs.Length == 0) return "English and Spanish titles are required.";
        if (req.DurationMinutes is < 1 or > 120) return "Duration must be between 1 and 120 minutes.";
        if (req.Reps is < 1 or > 1000) return "Reps must be between 1 and 1000, or left empty.";

        var video = string.IsNullOrWhiteSpace(req.VideoUrl) ? null : req.VideoUrl.Trim();
        if (video is not null
            && (!Uri.TryCreate(video, UriKind.Absolute, out var uri) || (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp)))
            return "Video link must be a full web address starting with https://.";

        drill.Category = category;
        drill.TitleEn = titleEn;
        drill.TitleEs = titleEs;
        drill.DescriptionEn = req.DescriptionEn?.Trim() ?? "";
        drill.DescriptionEs = req.DescriptionEs?.Trim() ?? "";
        drill.StepsEn = NormalizeSteps(req.StepsEn);
        drill.StepsEs = NormalizeSteps(req.StepsEs);
        drill.DurationMinutes = req.DurationMinutes;
        drill.Reps = req.Reps;
        drill.VideoUrl = video;
        drill.IsActive = req.IsActive;
        return null;
    }

    /// <summary>Trims each line and drops blank ones so stored steps line up by position.</summary>
    private static string NormalizeSteps(string? steps) =>
        string.Join('\n', (steps ?? "").Replace("\r", "")
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));

    private static AdminDrillDto ToDto(Drill d, int assignmentCount) => new(
        d.Id, d.Category.ToWire(), d.TitleEn, d.TitleEs, d.DescriptionEn, d.DescriptionEs,
        d.StepsEn, d.StepsEs, d.DurationMinutes, d.Reps, d.VideoUrl, d.IsActive, assignmentCount, d.UpdatedAt);
}
