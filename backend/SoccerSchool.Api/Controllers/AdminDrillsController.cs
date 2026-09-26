using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SoccerSchool.Api.Data;
using SoccerSchool.Api.Domain;
using SoccerSchool.Api.Dtos;
using SoccerSchool.Api.Services;

namespace SoccerSchool.Api.Controllers;

/// <summary>
/// Daily Training drills (bilingual) and their assignments — a library shared by all staff.
/// Permissions (Auth/Permissions.cs): <c>drills.view</c> to browse, <c>drills.create</c> ("Drill
/// creator", granted per person) to author and to edit one's own drills, <c>drills.edit</c> to edit
/// any drill, <c>drills.assign</c> to assign. Scope: admins assign to any player, team or age group;
/// everyone else only to the teams their coach card links them to and those teams' players.
/// Accepts the web cookie and the mobile JWT.
/// </summary>
[ApiController]
[Route("api/admin/drills")]
[Authorize(AuthenticationSchemes = AuthSchemes.CookieOrMobileJwt)]
public class AdminDrillsController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IPermissionService _permissions;

    public AdminDrillsController(AppDbContext db, IPermissionService permissions)
    {
        _db = db;
        _permissions = permissions;
    }

    // ---- Drills ----

    /// <summary>The shared library: every active drill. With <paramref name="includeArchived"/>,
    /// callers who can edit any drill also get every archived one; others get the archived drills
    /// they wrote. Assignment counts are limited to what the caller can see.</summary>
    [HttpGet]
    [RequirePermission(Permissions.DrillsView)]
    public async Task<ActionResult<IEnumerable<AdminDrillDto>>> List([FromQuery] bool includeArchived, CancellationToken ct)
    {
        var me = await CurrentAsync(ct);
        var visible = VisibleAssignments(me);
        var drills = await VisibleDrills(me, includeArchived)
            .AsNoTracking()
            .OrderBy(d => d.Category).ThenBy(d => d.TitleEn)
            .Select(d => new { Drill = d, AssignmentCount = visible.Count(a => a.DrillId == d.Id) })
            .ToListAsync(ct);
        var authors = await AuthorNamesAsync(drills.Select(x => x.Drill.CreatedByUserId), ct);
        return Ok(drills.Select(x => ToDto(x.Drill, x.AssignmentCount, authors, me)));
    }

    [HttpGet("{id:int}")]
    [RequirePermission(Permissions.DrillsView)]
    public async Task<ActionResult<AdminDrillDto>> Get(int id, CancellationToken ct)
    {
        var me = await CurrentAsync(ct);
        var drill = await VisibleDrills(me, includeArchived: true).AsNoTracking().FirstOrDefaultAsync(d => d.Id == id, ct);
        if (drill is null) return NotFound();
        var count = await VisibleAssignments(me).CountAsync(a => a.DrillId == id, ct);
        var authors = await AuthorNamesAsync(new[] { drill.CreatedByUserId }, ct);
        return Ok(ToDto(drill, count, authors, me));
    }

    /// <summary>Authors a drill; the author is recorded so they can edit it later.</summary>
    [HttpPost]
    [RequirePermission(Permissions.DrillsCreate)]
    public async Task<ActionResult<AdminDrillDto>> Create([FromBody] SaveDrillRequest req, CancellationToken ct)
    {
        var me = await CurrentAsync(ct);
        var drill = new Drill { CreatedByUserId = me.UserId };
        var error = Apply(drill, req);
        if (error is not null) return BadRequest(error);
        _db.Drills.Add(drill);
        await _db.SaveChangesAsync(ct);
        return await Get(drill.Id, ct);
    }

    [HttpPut("{id:int}")]
    [RequirePermission(Permissions.DrillsEdit, Permissions.DrillsCreate)]
    public async Task<ActionResult<AdminDrillDto>> Update(int id, [FromBody] SaveDrillRequest req, CancellationToken ct)
    {
        var me = await CurrentAsync(ct);
        var drill = await VisibleDrills(me, includeArchived: true).FirstOrDefaultAsync(d => d.Id == id, ct);
        if (drill is null) return NotFound();
        if (!CanEdit(drill, me)) return OnlyAuthorCanEdit();

        var error = Apply(drill, req);
        if (error is not null) return BadRequest(error);
        drill.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        return await Get(id, ct);
    }

    /// <summary>Hard delete, only for drills no kid has completed yet. Once there's history, the
    /// author archives instead (IsActive=false via PUT) so streaks and totals stay intact.</summary>
    [HttpDelete("{id:int}")]
    [RequirePermission(Permissions.DrillsEdit, Permissions.DrillsCreate)]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        var me = await CurrentAsync(ct);
        var drill = await VisibleDrills(me, includeArchived: true).FirstOrDefaultAsync(d => d.Id == id, ct);
        if (drill is null) return NotFound();
        if (!CanEdit(drill, me)) return OnlyAuthorCanEdit();
        if (await _db.DrillCompletions.AnyAsync(c => c.DrillId == id, ct))
            return Conflict("Players have already completed this drill. Archive it instead so their progress is kept.");
        _db.Drills.Remove(drill);
        await _db.SaveChangesAsync(ct);
        return NoContent();
    }

    // ---- Assignments ----

    /// <summary>Assignments the caller can see, optionally filtered to one drill. Newest first.</summary>
    [HttpGet("assignments")]
    [RequirePermission(Permissions.DrillsAssign)]
    public async Task<ActionResult<IEnumerable<AdminDrillAssignmentDto>>> ListAssignments([FromQuery] int? drillId, CancellationToken ct)
    {
        var me = await CurrentAsync(ct);
        return Ok(await LoadAssignmentsAsync(me, drillId, assignmentId: null, ct));
    }

    [HttpPost("assignments")]
    [RequirePermission(Permissions.DrillsAssign)]
    public async Task<ActionResult<AdminDrillAssignmentDto>> CreateAssignment([FromBody] CreateDrillAssignmentRequest req, CancellationToken ct)
    {
        var me = await CurrentAsync(ct);

        if (!TrainingWire.TryParseTargetType(req.TargetType, out var type))
            return BadRequest("Target type must be player, team or age-group.");
        if (req.EndDate is not null && req.EndDate < req.StartDate)
            return BadRequest("End date can't be before the start date.");
        var canSeeArchived = me.Has(Permissions.DrillsEdit);
        if (!await _db.Drills.AnyAsync(d => d.Id == req.DrillId && (canSeeArchived || d.IsActive), ct))
            return BadRequest("Drill not found.");

        var targetExists = type switch
        {
            DrillTargetType.Player => await _db.Players.AnyAsync(p => p.Id == req.TargetId, ct),
            DrillTargetType.Team => await _db.Teams.AnyAsync(t => t.Id == req.TargetId, ct),
            _ => await _db.AgeClassifications.AnyAsync(c => c.Id == req.TargetId, ct),
        };
        if (!targetExists) return BadRequest("The selected player, team or age group no longer exists.");
        if (!await CanTargetAsync(me, type, req.TargetId, ct)) return OutOfScope();

        var assignment = new DrillAssignment
        {
            DrillId = req.DrillId,
            TargetType = type,
            PlayerId = type == DrillTargetType.Player ? req.TargetId : null,
            TeamId = type == DrillTargetType.Team ? req.TargetId : null,
            AgeClassificationId = type == DrillTargetType.AgeClassification ? req.TargetId : null,
            StartDate = req.StartDate,
            EndDate = req.EndDate,
            CreatedByUserId = me.UserId,
        };
        _db.DrillAssignments.Add(assignment);
        await _db.SaveChangesAsync(ct);

        return Ok((await LoadAssignmentsAsync(me, drillId: null, assignment.Id, ct)).Single());
    }

    [HttpDelete("assignments/{id:int}")]
    [RequirePermission(Permissions.DrillsAssign)]
    public async Task<IActionResult> DeleteAssignment(int id, CancellationToken ct)
    {
        var me = await CurrentAsync(ct);
        // Callers only ever see (and so can only remove) assignments inside their scope; anything
        // else is a 404 to them, same as a missing row.
        var assignment = await VisibleAssignments(me).FirstOrDefaultAsync(a => a.Id == id, ct);
        if (assignment is null) return NotFound();
        _db.DrillAssignments.Remove(assignment);
        await _db.SaveChangesAsync(ct);
        return NoContent();
    }

    /// <summary>Picker options for the assignment form. Admins get every team and age group and
    /// search players through the admin players endpoint; coaches get their own teams plus every
    /// player rostered on them (a short list, so it's returned in full).</summary>
    [HttpGet("targets")]
    [RequirePermission(Permissions.DrillsAssign)]
    public async Task<ActionResult<DrillTargetOptionsDto>> Targets(CancellationToken ct)
    {
        var me = await CurrentAsync(ct);
        var teamIds = me.CoachTeamIds.ToList();
        var teams = await _db.Teams.AsNoTracking()
            .Where(t => me.IsAdmin || teamIds.Contains(t.Id))
            .OrderBy(t => t.Name)
            .Select(t => new DrillTargetOptionDto(t.Id, t.Name)).ToListAsync(ct);

        if (me.IsAdmin)
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

    /// <summary>The caller's effective permissions. Always present here: [RequirePermission] has
    /// already rejected anyone without them.</summary>
    private async Task<EffectivePermissions> CurrentAsync(CancellationToken ct) =>
        (await _permissions.GetAsync(User, ct))!;

    private ObjectResult OnlyAuthorCanEdit() =>
        StatusCode(StatusCodes.Status403Forbidden, "Only the person who created this drill (or an admin) can change it.");

    private ObjectResult OutOfScope() =>
        StatusCode(StatusCodes.Status403Forbidden, "You can only assign drills to your own teams and their players.");

    /// <summary>Drills the caller can see: every active drill; archived ones for those who can edit
    /// any drill, or for their author.</summary>
    private IQueryable<Drill> VisibleDrills(EffectivePermissions me, bool includeArchived)
    {
        var seeAllArchived = me.Has(Permissions.DrillsEdit);
        return _db.Drills.Where(d => d.IsActive
            || (includeArchived && (seeAllArchived || d.CreatedByUserId == me.UserId)));
    }

    /// <summary><c>drills.edit</c> edits any drill; a Drill creator edits only drills they wrote.</summary>
    private static bool CanEdit(Drill drill, EffectivePermissions me) =>
        me.Has(Permissions.DrillsEdit)
        || (me.Has(Permissions.DrillsCreate) && drill.CreatedByUserId == me.UserId);

    /// <summary>Display names for drill authors: parent-profile name if they have one, else email.</summary>
    private async Task<Dictionary<string, string>> AuthorNamesAsync(IEnumerable<string?> userIds, CancellationToken ct)
    {
        var ids = userIds.Where(id => !string.IsNullOrEmpty(id)).Select(id => id!).Distinct().ToList();
        if (ids.Count == 0) return new Dictionary<string, string>();
        var rows = await _db.Users
            .Where(u => ids.Contains(u.Id))
            .Select(u => new
            {
                u.Id,
                u.Email,
                First = _db.ParentAccounts.Where(a => a.UserId == u.Id).Select(a => a.FirstName).FirstOrDefault(),
                Last = _db.ParentAccounts.Where(a => a.UserId == u.Id).Select(a => a.LastName).FirstOrDefault(),
            })
            .ToListAsync(ct);
        return rows.ToDictionary(
            r => r.Id,
            r => ($"{r.First} {r.Last}").Trim() is { Length: > 0 } name ? name : (r.Email ?? "Unknown"));
    }

    /// <summary>Assignments within the caller's scope: everything for admins; otherwise those
    /// targeting one of their coached teams or a player rostered on one.</summary>
    private IQueryable<DrillAssignment> VisibleAssignments(EffectivePermissions me)
    {
        if (me.IsAdmin) return _db.DrillAssignments;
        var teamIds = me.CoachTeamIds.ToList();
        return _db.DrillAssignments.Where(a =>
            (a.TargetType == DrillTargetType.Team && a.TeamId != null && teamIds.Contains(a.TeamId.Value))
            || (a.TargetType == DrillTargetType.Player
                && _db.TeamPlayers.Any(tp => tp.PlayerId == a.PlayerId && teamIds.Contains(tp.TeamId))));
    }

    private async Task<bool> CanTargetAsync(EffectivePermissions me, DrillTargetType type, int targetId, CancellationToken ct)
    {
        if (me.IsAdmin) return true;
        var teamIds = me.CoachTeamIds.ToList();
        return type switch
        {
            DrillTargetType.Team => teamIds.Contains(targetId),
            DrillTargetType.Player => await _db.TeamPlayers.AnyAsync(tp => tp.PlayerId == targetId && teamIds.Contains(tp.TeamId), ct),
            // Age groups span every team, so only admins can target them.
            _ => false,
        };
    }

    private async Task<List<AdminDrillAssignmentDto>> LoadAssignmentsAsync(EffectivePermissions me, int? drillId, int? assignmentId, CancellationToken ct)
    {
        var rows = await VisibleAssignments(me)
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

    private static AdminDrillDto ToDto(Drill d, int assignmentCount, IReadOnlyDictionary<string, string> authors, EffectivePermissions me) => new(
        d.Id, d.Category.ToWire(), d.TitleEn, d.TitleEs, d.DescriptionEn, d.DescriptionEs,
        d.StepsEn, d.StepsEs, d.DurationMinutes, d.Reps, d.VideoUrl, d.IsActive, assignmentCount, d.UpdatedAt,
        d.CreatedByUserId is not null && authors.TryGetValue(d.CreatedByUserId, out var name) ? name : null,
        CanEdit(d, me));
}
