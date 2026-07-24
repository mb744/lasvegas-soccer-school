using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SoccerSchool.Api.Data;
using SoccerSchool.Api.Domain;
using SoccerSchool.Api.Dtos;

namespace SoccerSchool.Api.Controllers;

/// <summary>
/// Admin-side CRUD for tournaments/leagues that LVSS is HOSTING (as opposed to the participating
/// <see cref="Tournament"/> flow which tracks LVSS teams travelling to external tournaments).
/// Also owns the invited-teams catalog (external teams admin can roster into a hosted event
/// without retyping their coach contact each time) and the tournament↔team join CRUD.
/// </summary>
[ApiController]
[Route("api/admin/hosted-tournaments")]
[Authorize(Roles = Roles.Admin)]
public class AdminHostedTournamentsController : ControllerBase
{
    private readonly AppDbContext _db;

    public AdminHostedTournamentsController(AppDbContext db) => _db = db;

    // ------------------------------------------------------------
    // Hosted tournaments
    // ------------------------------------------------------------

    [HttpGet]
    public async Task<ActionResult<IEnumerable<HostedTournamentDto>>> List(CancellationToken ct)
    {
        var rows = await LoadTournamentQuery().OrderByDescending(t => t.StartDate).ToListAsync(ct);
        return Ok(rows.Select(ToDto).ToList());
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<HostedTournamentDto>> Get(int id, CancellationToken ct)
    {
        var row = await LoadTournamentQuery().FirstOrDefaultAsync(t => t.Id == id, ct);
        if (row is null) return NotFound();
        return Ok(ToDto(row));
    }

    [HttpPost]
    public async Task<ActionResult<HostedTournamentDto>> Create(
        [FromBody] SaveHostedTournamentRequest req, CancellationToken ct)
    {
        var error = await ValidateAsync(req, id: null, ct);
        if (error is not null) return BadRequest(error);

        var now = DateTime.UtcNow;
        var t = new HostedTournament
        {
            Name = req.Name.Trim(),
            Kind = req.Kind,
            StartDate = req.StartDate,
            EndDate = req.EndDate,
            VenueId = req.VenueId,
            Location = string.IsNullOrWhiteSpace(req.Location) ? null : req.Location!.Trim(),
            CostPerTeam = req.CostPerTeam,
            Notes = string.IsNullOrWhiteSpace(req.Notes) ? null : req.Notes!.Trim(),
            CreatedAt = now,
            UpdatedAt = now,
        };
        _db.HostedTournaments.Add(t);
        await _db.SaveChangesAsync(ct);
        return Ok(await LoadAndMap(t.Id, ct));
    }

    [HttpPut("{id:int}")]
    public async Task<ActionResult<HostedTournamentDto>> Update(
        int id, [FromBody] SaveHostedTournamentRequest req, CancellationToken ct)
    {
        var t = await _db.HostedTournaments.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (t is null) return NotFound();
        var error = await ValidateAsync(req, id, ct);
        if (error is not null) return BadRequest(error);

        t.Name = req.Name.Trim();
        t.Kind = req.Kind;
        t.StartDate = req.StartDate;
        t.EndDate = req.EndDate;
        t.VenueId = req.VenueId;
        t.Location = string.IsNullOrWhiteSpace(req.Location) ? null : req.Location!.Trim();
        t.CostPerTeam = req.CostPerTeam;
        t.Notes = string.IsNullOrWhiteSpace(req.Notes) ? null : req.Notes!.Trim();
        t.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        return Ok(await LoadAndMap(t.Id, ct));
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        var t = await _db.HostedTournaments.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (t is null) return NotFound();
        _db.HostedTournaments.Remove(t);
        await _db.SaveChangesAsync(ct);
        return NoContent();
    }

    // ------------------------------------------------------------
    // Tournament ↔ team roster
    // ------------------------------------------------------------

    [HttpPost("{id:int}/teams")]
    public async Task<ActionResult<HostedTournamentDto>> AddTeam(
        int id, [FromBody] AddHostedTournamentTeamRequest req, CancellationToken ct)
    {
        var t = await _db.HostedTournaments.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (t is null) return NotFound();
        var hasLvss = req.LvssTeamId.HasValue;
        var hasInvited = req.InvitedTeamId.HasValue;
        if (hasLvss == hasInvited)
            return BadRequest("Provide exactly one of LvssTeamId or InvitedTeamId.");
        if (hasLvss && !await _db.Teams.AnyAsync(x => x.Id == req.LvssTeamId, ct))
            return BadRequest("LVSS team not found.");
        if (hasInvited && !await _db.InvitedTeams.AnyAsync(x => x.Id == req.InvitedTeamId, ct))
            return BadRequest("Invited team not found.");
        // Prevent duplicate rosters — same team can only be added once per hosted event.
        var already = hasLvss
            ? await _db.HostedTournamentTeams.AnyAsync(r => r.HostedTournamentId == id && r.LvssTeamId == req.LvssTeamId, ct)
            : await _db.HostedTournamentTeams.AnyAsync(r => r.HostedTournamentId == id && r.InvitedTeamId == req.InvitedTeamId, ct);
        if (already) return Conflict("That team is already on this tournament.");

        _db.HostedTournamentTeams.Add(new HostedTournamentTeam
        {
            HostedTournamentId = id,
            LvssTeamId = req.LvssTeamId,
            InvitedTeamId = req.InvitedTeamId,
            Notes = string.IsNullOrWhiteSpace(req.Notes) ? null : req.Notes!.Trim(),
            CreatedAt = DateTime.UtcNow,
        });
        await _db.SaveChangesAsync(ct);
        return Ok(await LoadAndMap(id, ct));
    }

    [HttpDelete("{id:int}/teams/{teamRowId:int}")]
    public async Task<ActionResult<HostedTournamentDto>> RemoveTeam(int id, int teamRowId, CancellationToken ct)
    {
        var row = await _db.HostedTournamentTeams
            .FirstOrDefaultAsync(x => x.Id == teamRowId && x.HostedTournamentId == id, ct);
        if (row is null) return NotFound();
        _db.HostedTournamentTeams.Remove(row);
        await _db.SaveChangesAsync(ct);
        return Ok(await LoadAndMap(id, ct));
    }

    // ------------------------------------------------------------
    // Invited teams catalog
    // ------------------------------------------------------------

    [HttpGet("~/api/admin/invited-teams")]
    public async Task<ActionResult<IEnumerable<InvitedTeamDto>>> ListInvited(CancellationToken ct)
    {
        var rows = await _db.InvitedTeams.AsNoTracking().OrderBy(t => t.Name).ToListAsync(ct);
        return Ok(rows.Select(ToInvitedDto).ToList());
    }

    [HttpPost("~/api/admin/invited-teams")]
    public async Task<ActionResult<InvitedTeamDto>> CreateInvited(
        [FromBody] SaveInvitedTeamRequest req, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.Name)) return BadRequest("Name is required.");
        var now = DateTime.UtcNow;
        var t = new InvitedTeam
        {
            Name = req.Name.Trim(),
            HeadCoachName = TrimOrNull(req.HeadCoachName),
            HeadCoachPhone = TrimOrNull(req.HeadCoachPhone),
            HeadCoachEmail = TrimOrNull(req.HeadCoachEmail),
            AgeGroup = TrimOrNull(req.AgeGroup),
            Notes = TrimOrNull(req.Notes),
            CreatedAt = now,
            UpdatedAt = now,
        };
        _db.InvitedTeams.Add(t);
        await _db.SaveChangesAsync(ct);
        return Ok(ToInvitedDto(t));
    }

    [HttpPut("~/api/admin/invited-teams/{id:int}")]
    public async Task<ActionResult<InvitedTeamDto>> UpdateInvited(
        int id, [FromBody] SaveInvitedTeamRequest req, CancellationToken ct)
    {
        var t = await _db.InvitedTeams.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (t is null) return NotFound();
        if (string.IsNullOrWhiteSpace(req.Name)) return BadRequest("Name is required.");
        t.Name = req.Name.Trim();
        t.HeadCoachName = TrimOrNull(req.HeadCoachName);
        t.HeadCoachPhone = TrimOrNull(req.HeadCoachPhone);
        t.HeadCoachEmail = TrimOrNull(req.HeadCoachEmail);
        t.AgeGroup = TrimOrNull(req.AgeGroup);
        t.Notes = TrimOrNull(req.Notes);
        t.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        return Ok(ToInvitedDto(t));
    }

    [HttpDelete("~/api/admin/invited-teams/{id:int}")]
    public async Task<IActionResult> DeleteInvited(int id, CancellationToken ct)
    {
        var t = await _db.InvitedTeams.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (t is null) return NotFound();
        // Block delete when the team is on any hosted tournament — force the admin to remove
        // roster rows first so the participation history isn't quietly orphaned.
        var inUse = await _db.HostedTournamentTeams.AnyAsync(r => r.InvitedTeamId == id, ct);
        if (inUse) return Conflict("This invited team is rostered on one or more hosted tournaments — remove it there first.");
        _db.InvitedTeams.Remove(t);
        await _db.SaveChangesAsync(ct);
        return NoContent();
    }

    // ------------------------------------------------------------
    // Helpers
    // ------------------------------------------------------------

    private IQueryable<HostedTournament> LoadTournamentQuery() =>
        _db.HostedTournaments
            .AsNoTracking()
            .Include(t => t.Venue)
            .Include(t => t.Teams).ThenInclude(r => r.LvssTeam)
            .Include(t => t.Teams).ThenInclude(r => r.InvitedTeam);

    private async Task<HostedTournamentDto> LoadAndMap(int id, CancellationToken ct)
    {
        var row = await LoadTournamentQuery().FirstAsync(t => t.Id == id, ct);
        return ToDto(row);
    }

    private async Task<string?> ValidateAsync(SaveHostedTournamentRequest req, int? id, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.Name)) return "Name is required.";
        if (req.StartDate == default) return "Start date is required.";
        if (req.EndDate is DateOnly e && e < req.StartDate) return "End date can't be before the start date.";
        if (req.VenueId is int v && !await _db.Venues.AnyAsync(x => x.Id == v, ct)) return "Venue not found.";
        _ = id; // reserved for future name-uniqueness checks
        return null;
    }

    private static string? TrimOrNull(string? s) =>
        string.IsNullOrWhiteSpace(s) ? null : s.Trim();

    private static HostedTournamentDto ToDto(HostedTournament t) =>
        new(
            t.Id, t.Name, t.Kind, t.StartDate, t.EndDate,
            t.VenueId, t.Venue?.Name, t.Venue?.Address,
            t.Location, t.CostPerTeam, t.Notes,
            t.CreatedAt, t.UpdatedAt,
            t.Teams
                .OrderBy(r => r.LvssTeam?.Name ?? r.InvitedTeam?.Name ?? string.Empty)
                .Select(r => new HostedTournamentTeamDto(
                    r.Id,
                    r.LvssTeamId, r.LvssTeam?.Name,
                    r.InvitedTeamId, r.InvitedTeam?.Name,
                    r.InvitedTeam?.AgeGroup,
                    r.InvitedTeam?.HeadCoachName,
                    r.InvitedTeam?.HeadCoachPhone,
                    r.InvitedTeam?.HeadCoachEmail,
                    r.Notes, r.CreatedAt))
                .ToList());

    private static InvitedTeamDto ToInvitedDto(InvitedTeam t) =>
        new(t.Id, t.Name, t.HeadCoachName, t.HeadCoachPhone, t.HeadCoachEmail,
            t.AgeGroup, t.Notes, t.CreatedAt, t.UpdatedAt);
}
