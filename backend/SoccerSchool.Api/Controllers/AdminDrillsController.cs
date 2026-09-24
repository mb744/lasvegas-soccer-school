using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SoccerSchool.Api.Data;
using SoccerSchool.Api.Domain;
using SoccerSchool.Api.Dtos;

namespace SoccerSchool.Api.Controllers;

/// <summary>
/// Admin authoring of Daily Training drills (bilingual) and assigning them to a player, a team, or
/// an age group for a date range. Accepts the web cookie and the parent app's admin JWT.
/// </summary>
[ApiController]
[Route("api/admin/drills")]
[Authorize(Roles = Roles.Admin, AuthenticationSchemes = AuthSchemes.CookieOrMobileJwt)]
public class AdminDrillsController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly UserManager<ApplicationUser> _users;

    public AdminDrillsController(AppDbContext db, UserManager<ApplicationUser> users)
    {
        _db = db;
        _users = users;
    }

    // ---- Drills ----

    [HttpGet]
    public async Task<ActionResult<IEnumerable<AdminDrillDto>>> List([FromQuery] bool includeArchived, CancellationToken ct)
    {
        var drills = await _db.Drills
            .AsNoTracking()
            .Where(d => includeArchived || d.IsActive)
            .OrderBy(d => d.Category).ThenBy(d => d.TitleEn)
            .Select(d => new { Drill = d, AssignmentCount = d.Assignments.Count })
            .ToListAsync(ct);
        return Ok(drills.Select(x => ToDto(x.Drill, x.AssignmentCount)));
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<AdminDrillDto>> Get(int id, CancellationToken ct)
    {
        var drill = await _db.Drills.AsNoTracking().FirstOrDefaultAsync(d => d.Id == id, ct);
        if (drill is null) return NotFound();
        var count = await _db.DrillAssignments.CountAsync(a => a.DrillId == id, ct);
        return Ok(ToDto(drill, count));
    }

    [HttpPost]
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

    /// <summary>Assignments, optionally filtered to one drill. Newest first.</summary>
    [HttpGet("assignments")]
    public async Task<ActionResult<IEnumerable<AdminDrillAssignmentDto>>> ListAssignments([FromQuery] int? drillId, CancellationToken ct) =>
        Ok(await LoadAssignmentsAsync(drillId, assignmentId: null, ct));

    private async Task<List<AdminDrillAssignmentDto>> LoadAssignmentsAsync(int? drillId, int? assignmentId, CancellationToken ct)
    {
        var rows = await _db.DrillAssignments
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

    [HttpPost("assignments")]
    public async Task<ActionResult<AdminDrillAssignmentDto>> CreateAssignment([FromBody] CreateDrillAssignmentRequest req, CancellationToken ct)
    {
        if (!TrainingWire.TryParseTargetType(req.TargetType, out var type))
            return BadRequest("Target type must be player, team or age-group.");
        if (req.EndDate is not null && req.EndDate < req.StartDate)
            return BadRequest("End date can't be before the start date.");
        if (!await _db.Drills.AnyAsync(d => d.Id == req.DrillId, ct))
            return BadRequest("Drill not found.");

        var targetExists = type switch
        {
            DrillTargetType.Player => await _db.Players.AnyAsync(p => p.Id == req.TargetId, ct),
            DrillTargetType.Team => await _db.Teams.AnyAsync(t => t.Id == req.TargetId, ct),
            _ => await _db.AgeClassifications.AnyAsync(c => c.Id == req.TargetId, ct),
        };
        if (!targetExists) return BadRequest("The selected player, team or age group no longer exists.");

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

        return Ok((await LoadAssignmentsAsync(drillId: null, assignment.Id, ct)).Single());
    }

    [HttpDelete("assignments/{id:int}")]
    public async Task<IActionResult> DeleteAssignment(int id, CancellationToken ct)
    {
        var assignment = await _db.DrillAssignments.FirstOrDefaultAsync(a => a.Id == id, ct);
        if (assignment is null) return NotFound();
        _db.DrillAssignments.Remove(assignment);
        await _db.SaveChangesAsync(ct);
        return NoContent();
    }

    /// <summary>Teams and age groups for the assignment picker. Players are searched through the
    /// existing admin players endpoint.</summary>
    [HttpGet("targets")]
    public async Task<ActionResult<DrillTargetOptionsDto>> Targets(CancellationToken ct)
    {
        var teams = await _db.Teams.AsNoTracking().OrderBy(t => t.Name)
            .Select(t => new DrillTargetOptionDto(t.Id, t.Name)).ToListAsync(ct);
        var ages = await _db.AgeClassifications.AsNoTracking().OrderBy(c => c.Name)
            .Select(c => new DrillTargetOptionDto(c.Id, c.Name)).ToListAsync(ct);
        return Ok(new DrillTargetOptionsDto(teams, ages));
    }

    // ---------------------------------------------------------------------------------------

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
