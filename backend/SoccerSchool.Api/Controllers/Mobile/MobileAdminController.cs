using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SoccerSchool.Api.Data;
using SoccerSchool.Api.Domain;

namespace SoccerSchool.Api.Controllers.Mobile;

/// <summary>
/// Read-only + light-touch admin surface for the mobile app. Everything under
/// <c>/api/mobile/admin/*</c> is Admin-role gated over the mobile JWT. Heavier CRUD (bulk player
/// import, tournament brackets, template editing) stays on the web admin — this controller only
/// covers the on-the-go workflows: seeing rosters, cancelling an event, poking at chat groups.
/// </summary>
[ApiController]
[Route("api/mobile/admin")]
[Authorize(Roles = Roles.Admin, AuthenticationSchemes = AuthSchemes.CookieOrMobileJwt)]
public class MobileAdminController : ControllerBase
{
    private readonly AppDbContext _db;

    public MobileAdminController(AppDbContext db) { _db = db; }

    /// <summary>Team picker for the announcement composer + admin teams list — id + name only.</summary>
    [HttpGet("teams")]
    public async Task<ActionResult<IEnumerable<MobileTeamOptionDto>>> Teams(CancellationToken ct)
    {
        var rows = await _db.Teams
            .OrderBy(t => t.Name)
            .Select(t => new MobileTeamOptionDto(t.Id, t.Name))
            .ToListAsync(ct);
        return Ok(rows);
    }

    /// <summary>Team detail — roster + coaches. Read-only; roster edits still happen on the web
    /// admin's team page (drag-and-drop, bulk player merge) which doesn't translate well to phone.</summary>
    [HttpGet("teams/{id:int}")]
    public async Task<ActionResult<MobileAdminTeamDetailDto>> TeamDetail(int id, CancellationToken ct)
    {
        var team = await _db.Teams
            .Where(t => t.Id == id)
            .Select(t => new { t.Id, t.Name })
            .FirstOrDefaultAsync(ct);
        if (team is null) return NotFound();

        var players = await _db.TeamPlayers
            .Where(tp => tp.TeamId == id)
            .OrderBy(tp => tp.Player!.FirstName).ThenBy(tp => tp.Player!.LastName)
            .Select(tp => new MobileAdminTeamPlayerDto(
                tp.PlayerId,
                tp.Player!.FirstName,
                tp.Player.LastName,
                tp.Player.DateOfBirth,
                tp.Player.ParentAccount != null
                    ? (tp.Player.ParentAccount!.FirstName + " " + tp.Player.ParentAccount.LastName).Trim()
                    : null,
                tp.Player.ParentAccount != null ? tp.Player.ParentAccount!.CellPhone : null))
            .ToListAsync(ct);

        var coaches = await _db.TeamCoaches
            .Where(tc => tc.TeamId == id)
            .OrderBy(tc => tc.Role).ThenBy(tc => tc.Name)
            .Select(tc => new MobileAdminTeamCoachDto(
                tc.Id, tc.Name, tc.Email, tc.Phone, tc.Role.ToString()))
            .ToListAsync(ct);

        return Ok(new MobileAdminTeamDetailDto(team.Id, team.Name, players, coaches));
    }

    /// <summary>Upcoming (and just-past) events across every team, admin overview. Optionally
    /// filter by team.</summary>
    [HttpGet("events")]
    public async Task<ActionResult<IEnumerable<MobileAdminEventDto>>> Events(
        CancellationToken ct, [FromQuery] int? teamId = null)
    {
        var fromUtc = DateTime.UtcNow.AddDays(-1);
        var toUtc = DateTime.UtcNow.AddDays(60);

        var rows = await _db.ScheduledGames
            .Where(g => g.StartsAt >= fromUtc && g.StartsAt <= toUtc)
            .Where(g => teamId == null || g.TeamId == teamId)
            .OrderBy(g => g.StartsAt)
            .Select(g => new MobileAdminEventDto(
                g.Id, g.TeamId, g.Team!.Name, g.Kind, g.StartsAt, g.ArriveAt,
                g.OpponentName, g.Location, g.Venue != null ? g.Venue.Name : null, g.IsCancelled))
            .ToListAsync(ct);
        return Ok(rows);
    }

    /// <summary>Toggle cancel state on an event. Mirrors the web cancel button — sets IsCancelled
    /// and stamps CancelledAt. Uncancel clears both.</summary>
    [HttpPost("events/{id:int}/cancel")]
    public async Task<ActionResult<MobileAdminEventDto>> CancelEvent(int id, CancellationToken ct)
    {
        var ev = await _db.ScheduledGames.Include(g => g.Team).FirstOrDefaultAsync(g => g.Id == id, ct);
        if (ev is null) return NotFound();
        ev.IsCancelled = true;
        ev.CancelledAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        return Ok(ToEventDto(ev));
    }

    [HttpPost("events/{id:int}/uncancel")]
    public async Task<ActionResult<MobileAdminEventDto>> UncancelEvent(int id, CancellationToken ct)
    {
        var ev = await _db.ScheduledGames.Include(g => g.Team).FirstOrDefaultAsync(g => g.Id == id, ct);
        if (ev is null) return NotFound();
        ev.IsCancelled = false;
        ev.CancelledAt = null;
        await _db.SaveChangesAsync(ct);
        return Ok(ToEventDto(ev));
    }

    private static MobileAdminEventDto ToEventDto(ScheduledGame g) => new(
        g.Id, g.TeamId, g.Team?.Name ?? string.Empty, g.Kind, g.StartsAt, g.ArriveAt,
        g.OpponentName, g.Location, null, g.IsCancelled);

    /// <summary>All players an admin can pick from when adding to a team roster. Read-only; player
    /// details themselves are managed via the web admin.</summary>
    [HttpGet("players")]
    public async Task<ActionResult<IEnumerable<MobileAdminPlayerOptionDto>>> Players(
        CancellationToken ct, [FromQuery] string? q = null)
    {
        var query = _db.Players.AsQueryable();
        if (!string.IsNullOrWhiteSpace(q))
        {
            var needle = q.Trim();
            query = query.Where(p =>
                EF.Functions.Like(p.FirstName, $"%{needle}%") ||
                EF.Functions.Like(p.LastName, $"%{needle}%") ||
                EF.Functions.Like(p.FirstName + " " + p.LastName, $"%{needle}%"));
        }
        var rows = await query
            .OrderBy(p => p.LastName).ThenBy(p => p.FirstName)
            .Take(50)
            .Select(p => new MobileAdminPlayerOptionDto(
                p.Id, p.FirstName, p.LastName, p.DateOfBirth,
                p.ParentAccount != null ? (p.ParentAccount!.FirstName + " " + p.ParentAccount.LastName).Trim() : null))
            .ToListAsync(ct);
        return Ok(rows);
    }

    /// <summary>Every uniform for the game form's uniform picker.</summary>
    [HttpGet("uniforms")]
    public async Task<ActionResult<IEnumerable<MobileAdminUniformDto>>> Uniforms(CancellationToken ct)
    {
        var rows = await _db.Uniforms
            .OrderBy(u => u.Name)
            .Select(u => new MobileAdminUniformDto(u.Id, u.Name))
            .ToListAsync(ct);
        return Ok(rows);
    }

    /// <summary>Every venue for the event forms' venue picker.</summary>
    [HttpGet("venues")]
    public async Task<ActionResult<IEnumerable<MobileAdminVenueDto>>> Venues(CancellationToken ct)
    {
        var rows = await _db.Venues
            .OrderBy(v => v.Name)
            .Select(v => new MobileAdminVenueDto(v.Id, v.Name, v.Address))
            .ToListAsync(ct);
        return Ok(rows);
    }
}

public record MobileTeamOptionDto(int Id, string Name);

public record MobileAdminTeamDetailDto(
    int Id,
    string Name,
    IReadOnlyList<MobileAdminTeamPlayerDto> Players,
    IReadOnlyList<MobileAdminTeamCoachDto> Coaches);

public record MobileAdminTeamPlayerDto(
    int Id,
    string FirstName,
    string LastName,
    DateOnly DateOfBirth,
    string? ParentName,
    string? ParentPhone);

public record MobileAdminTeamCoachDto(
    int Id,
    string Name,
    string? Email,
    string? Phone,
    string Role);

public record MobileAdminEventDto(
    int Id,
    int TeamId,
    string TeamName,
    ScheduledEventKind Kind,
    DateTime StartsAt,
    DateTime? ArriveAt,
    string? OpponentName,
    string? Location,
    string? VenueName,
    bool IsCancelled);

public record MobileAdminPlayerOptionDto(
    int Id,
    string FirstName,
    string LastName,
    DateOnly DateOfBirth,
    string? ParentName);

public record MobileAdminUniformDto(int Id, string Name);
public record MobileAdminVenueDto(int Id, string Name, string? Address);
