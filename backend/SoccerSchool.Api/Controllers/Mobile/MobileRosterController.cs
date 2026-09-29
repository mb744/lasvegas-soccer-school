using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SoccerSchool.Api.Data;
using SoccerSchool.Api.Domain;
using SoccerSchool.Api.Dtos;
using SoccerSchool.Api.Services;

namespace SoccerSchool.Api.Controllers.Mobile;

/// <summary>
/// The app's Roster tab for staff: team list, a team's players with jersey numbers, and one
/// player's parents/guardians with contact details. Admins see every team; coaches only the teams
/// their coach card or coach profile links them to.
/// </summary>
[ApiController]
[Route("api/mobile/roster")]
[Authorize(AuthenticationSchemes = AuthSchemes.CookieOrMobileJwt)]
[RequirePermission(Permissions.PlayersView)]
public class MobileRosterController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly ICoachScopeService _coaches;

    public MobileRosterController(AppDbContext db, ICoachScopeService coaches)
    {
        _db = db;
        _coaches = coaches;
    }

    [HttpGet("teams")]
    public async Task<ActionResult<IEnumerable<MobileRosterTeamDto>>> Teams(CancellationToken ct)
    {
        var scope = await _coaches.GetScopeAsync(User, ct);
        var q = _db.Teams.AsQueryable();
        if (!scope.IsAdmin)
        {
            var ids = scope.CoachTeamIds.ToList();
            q = q.Where(t => ids.Contains(t.Id));
        }
        var teams = await q
            .OrderBy(t => t.Name)
            .Select(t => new MobileRosterTeamDto(t.Id, t.Name, t.Roster.Count))
            .ToListAsync(ct);
        return Ok(teams);
    }

    [HttpGet("teams/{teamId:int}")]
    public async Task<ActionResult<MobileRosterDto>> Roster(int teamId, CancellationToken ct)
    {
        if (!await CanSeeTeamAsync(teamId, ct)) return Forbid();
        var name = await _db.Teams.Where(t => t.Id == teamId).Select(t => t.Name).FirstOrDefaultAsync(ct);
        if (name is null) return NotFound();

        var players = await _db.TeamPlayers
            .Where(tp => tp.TeamId == teamId)
            .Select(tp => new { tp.Player!.Id, tp.Player.FirstName, tp.Player.LastName })
            .ToListAsync(ct);
        var jerseys = await JerseysAsync(players.Select(p => p.Id).ToList(), ct);

        var rows = players
            .OrderBy(p => p.LastName).ThenBy(p => p.FirstName)
            .Select(p => new MobileRosterPlayerDto(p.Id, p.FirstName, p.LastName,
                jerseys.TryGetValue(p.Id, out var j) ? j : Array.Empty<string>()))
            .ToList();
        return Ok(new MobileRosterDto(teamId, name, rows));
    }

    [HttpGet("teams/{teamId:int}/players/{playerId:int}")]
    public async Task<ActionResult<MobileRosterPlayerDetailDto>> Player(int teamId, int playerId, CancellationToken ct)
    {
        if (!await CanSeeTeamAsync(teamId, ct)) return Forbid();

        // The player must be on this team — a coach can't read other teams' families via a guessed id.
        var row = await _db.TeamPlayers
            .Where(tp => tp.TeamId == teamId && tp.PlayerId == playerId)
            .Select(tp => new
            {
                tp.Player!.FirstName, tp.Player.LastName, tp.Player.ParentAccountId,
                TeamName = tp.Team!.Name,
            })
            .FirstOrDefaultAsync(ct);
        if (row is null) return NotFound();

        var guardians = new List<MobileRosterGuardianDto>();
        var primary = await _db.ParentAccounts
            .Where(a => a.Id == row.ParentAccountId)
            .Select(a => new { a.FirstName, a.LastName, a.CellPhone, Email = a.User != null ? a.User.Email : null, a.Language })
            .FirstOrDefaultAsync(ct);
        if (primary is not null)
            guardians.Add(new MobileRosterGuardianDto($"{primary.FirstName} {primary.LastName}".Trim(), true,
                Blank(primary.CellPhone), Blank(primary.Email), primary.Language));

        // Additional parents/guardians on the family. View-only members (grandparents, friends)
        // aren't the child's parents and aren't listed.
        var contacts = await _db.ParentContacts
            .Where(c => c.ParentAccountId == row.ParentAccountId && c.AccessLevel == FamilyAccessLevel.Guardian)
            .OrderBy(c => c.CreatedAt)
            .Select(c => new { c.FirstName, c.LastName, c.CellPhone, c.Email, c.Language })
            .ToListAsync(ct);
        foreach (var c in contacts)
        {
            var phone = Blank(c.CellPhone);
            var email = Blank(c.Email);
            // Skip a contact row that just repeats the primary parent's own phone/email.
            if (guardians.Any(g => (phone != null && g.Phone == phone) || (email != null && string.Equals(g.Email, email, StringComparison.OrdinalIgnoreCase))))
                continue;
            guardians.Add(new MobileRosterGuardianDto($"{c.FirstName} {c.LastName}".Trim(), false, phone, email, c.Language));
        }

        var jerseys = await JerseysAsync(new List<int> { playerId }, ct);
        return Ok(new MobileRosterPlayerDetailDto(playerId, row.FirstName, row.LastName, teamId, row.TeamName,
            jerseys.TryGetValue(playerId, out var j) ? j : Array.Empty<string>(), guardians));
    }

    private async Task<bool> CanSeeTeamAsync(int teamId, CancellationToken ct)
    {
        var scope = await _coaches.GetScopeAsync(User, ct);
        return scope.IsAdmin || scope.CoachTeamIds.Contains(teamId);
    }

    /// <summary>Distinct jersey numbers on each player's active (not returned) uniforms.</summary>
    private async Task<Dictionary<int, IReadOnlyList<string>>> JerseysAsync(List<int> playerIds, CancellationToken ct)
    {
        if (playerIds.Count == 0) return new();
        var rows = await _db.PlayerUniformAssignments
            .Where(a => playerIds.Contains(a.PlayerId) && a.ReturnedAt == null && a.JerseyNumber != "")
            .OrderBy(a => a.AssignedAt)
            .Select(a => new { a.PlayerId, a.JerseyNumber })
            .ToListAsync(ct);
        return rows
            .GroupBy(r => r.PlayerId)
            .ToDictionary(g => g.Key,
                g => (IReadOnlyList<string>)g.Select(r => r.JerseyNumber.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).ToList());
    }

    private static string? Blank(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();
}
