using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SoccerSchool.Api.Data;
using SoccerSchool.Api.Domain;
using SoccerSchool.Api.Dtos;
using SoccerSchool.Api.Options;

namespace SoccerSchool.Api.Controllers;

/// <summary>
/// Admin roster builder. Operates on the same <see cref="Team"/> rows as the schedule/messaging
/// flows, so a team created here can later gain GotSport IDs (for schedule sync) and is already
/// targetable in the Compose tab via its roster (see RecipientResolver's <c>team-{id}</c> key).
/// Teams created here have GotSport IDs left at 0 ("not linked"); schedule sync stays disabled
/// for them until those IDs are filled in from the schedule UI.
/// </summary>
[ApiController]
[Route("api/teams")]
[Authorize(Roles = Roles.Admin)]
public class TeamsController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly AppOptions _app;

    public TeamsController(AppDbContext db, IOptions<AppOptions> app)
    {
        _db = db;
        _app = app.Value;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<RosterTeamSummary>>> List(CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var rows = await _db.Teams
            .Include(t => t.MessageGroup)
            .OrderBy(t => t.Name)
            .Select(t => new RosterTeamSummary(
                t.Id, t.Name,
                t.Roster.Count,
                t.Games.Count(g => g.StartsAt >= now && !g.IsCancelled),
                t.GotSportEventId > 0 && t.GotSportTeamId > 0,
                t.MessageGroupId,
                t.MessageGroup != null ? t.MessageGroup.Name : null,
                t.CreatedAt))
            .ToListAsync(ct);
        return Ok(rows);
    }

    [HttpPost]
    public async Task<ActionResult<RosterTeamSummary>> Create(
        [FromBody] CreateRosterTeamRequest request, CancellationToken ct)
    {
        var name = request.Name?.Trim();
        if (string.IsNullOrWhiteSpace(name)) return BadRequest("Name is required.");
        if (await _db.Teams.AnyAsync(t => t.Name == name, ct))
            return Conflict($"A team named '{name}' already exists.");

        var team = new Team { Name = name };
        _db.Teams.Add(team);
        await _db.SaveChangesAsync(ct);
        return Ok(await SummarizeAsync(team.Id, ct));
    }

    [HttpPut("{id:int}")]
    public async Task<ActionResult<RosterTeamSummary>> Rename(
        int id, [FromBody] RenameTeamRequest request, CancellationToken ct)
    {
        var team = await _db.Teams.FindAsync(new object?[] { id }, ct);
        if (team is null) return NotFound();

        var name = request.Name?.Trim();
        if (string.IsNullOrWhiteSpace(name)) return BadRequest("Name is required.");
        if (await _db.Teams.AnyAsync(t => t.Name == name && t.Id != id, ct))
            return Conflict($"A team named '{name}' already exists.");

        team.Name = name;
        await _db.SaveChangesAsync(ct);
        return Ok(await SummarizeAsync(team.Id, ct));
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        var team = await _db.Teams.FindAsync(new object?[] { id }, ct);
        if (team is null) return NotFound();
        _db.Teams.Remove(team);
        await _db.SaveChangesAsync(ct);
        return NoContent();
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<RosterTeamDetail>> Get(int id, CancellationToken ct)
    {
        var team = await _db.Teams
            .Include(t => t.MessageGroup)
            .Include(t => t.Roster).ThenInclude(tp => tp.Player!).ThenInclude(p => p.ParentAccount!).ThenInclude(pa => pa.User)
            .Include(t => t.Games)
            .FirstOrDefaultAsync(t => t.Id == id, ct);
        if (team is null) return NotFound();

        var playerIds = team.Roster.Select(tp => tp.PlayerId).ToList();
        var ageByPlayer = await AgeBracketByPlayerAsync(playerIds, ct);

        var roster = team.Roster
            .OrderBy(tp => tp.Player!.LastName).ThenBy(tp => tp.Player!.FirstName)
            .Select(tp =>
            {
                var p = tp.Player!;
                var pa = p.ParentAccount;
                var parentName = pa is null ? null : $"{pa.FirstName} {pa.LastName}".Trim();
                ageByPlayer.TryGetValue(p.Id, out var bracket);
                return new RosterMemberDto(
                    p.Id, p.FirstName, p.LastName, p.DateOfBirth,
                    bracket,
                    string.IsNullOrWhiteSpace(parentName) ? null : parentName,
                    pa?.CellPhone,
                    pa?.User?.Email,
                    tp.AddedAt);
            })
            .ToList();

        var now = DateTime.UtcNow;
        var games = team.Games
            .Where(g => g.StartsAt >= now.AddDays(-1))
            .OrderBy(g => g.StartsAt)
            .Select(g => new ScheduledGameDto(
                g.Id, team.Id, team.Name, team.MessageGroupId, team.MessageGroup?.Name,
                g.Kind, g.StartsAt, g.EndsAt, g.Summary, g.Location, g.Description,
                g.OpponentName, g.IsHome, g.SeriesId, g.IsCancelled, g.CancelledAt))
            .ToList();

        return Ok(new RosterTeamDetail(
            team.Id, team.Name, team.MessageGroupId, team.MessageGroup?.Name,
            team.GotSportEventId > 0 && team.GotSportTeamId > 0,
            team.GotSportEventId, team.GotSportTeamId, team.LastSyncedAt, team.LastSyncMessage,
            team.CreatedAt, roster, games));
    }

    /// <summary>Registered players (in <paramref name="season"/>, default = active season) not yet
    /// on this team's roster, with their age bracket + parent name from their most-recent
    /// registration so the admin can build the roster by age group.</summary>
    [HttpGet("{id:int}/available-players")]
    public async Task<ActionResult<IEnumerable<AvailablePlayerDto>>> AvailablePlayers(
        int id, [FromQuery] string? season, CancellationToken ct)
    {
        if (!await _db.Teams.AnyAsync(t => t.Id == id, ct)) return NotFound();

        var s = string.IsNullOrWhiteSpace(season) ? _app.ActiveSeason : season.Trim();
        var onTeam = await _db.TeamPlayers.Where(tp => tp.TeamId == id).Select(tp => tp.PlayerId).ToListAsync(ct);

        // Most-recent registration first, so the per-player dedupe below keeps the freshest bracket/parent.
        var rows = await _db.RegistrationPlayers
            .Where(rp => rp.Registration!.Season == s && !onTeam.Contains(rp.PlayerId))
            .OrderByDescending(rp => rp.Registration!.CreatedAt)
            .Select(rp => new
            {
                rp.PlayerId,
                rp.Player!.FirstName,
                rp.Player.LastName,
                rp.Player.DateOfBirth,
                Bracket = rp.AgeClassification != null ? rp.AgeClassification.Name : null,
                ParentFirst = rp.Registration!.ParentFirstName,
                ParentLast = rp.Registration.ParentLastName,
            })
            .ToListAsync(ct);

        var seen = new HashSet<int>();
        var result = new List<AvailablePlayerDto>();
        foreach (var r in rows)
        {
            if (!seen.Add(r.PlayerId)) continue;
            var parentName = $"{r.ParentFirst} {r.ParentLast}".Trim();
            result.Add(new AvailablePlayerDto(
                r.PlayerId, r.FirstName, r.LastName, r.DateOfBirth, r.Bracket,
                string.IsNullOrWhiteSpace(parentName) ? null : parentName));
        }
        result.Sort((a, b) =>
        {
            var c = string.Compare(a.LastName, b.LastName, StringComparison.OrdinalIgnoreCase);
            return c != 0 ? c : string.Compare(a.FirstName, b.FirstName, StringComparison.OrdinalIgnoreCase);
        });
        return Ok(result);
    }

    [HttpPost("{id:int}/roster")]
    public async Task<ActionResult<RosterTeamDetail>> AddMembers(
        int id, [FromBody] AddRosterMembersRequest request, CancellationToken ct)
    {
        if (!await _db.Teams.AnyAsync(t => t.Id == id, ct)) return NotFound();

        var requested = (request.PlayerIds ?? Array.Empty<int>()).Distinct().ToList();
        if (requested.Count == 0) return await Get(id, ct);

        // Only add players that exist and aren't already on the roster (unique index also guards this).
        var validPlayerIds = await _db.Players.Where(p => requested.Contains(p.Id)).Select(p => p.Id).ToListAsync(ct);
        var already = await _db.TeamPlayers
            .Where(tp => tp.TeamId == id && requested.Contains(tp.PlayerId))
            .Select(tp => tp.PlayerId)
            .ToListAsync(ct);

        foreach (var pid in validPlayerIds.Except(already))
            _db.TeamPlayers.Add(new TeamPlayer { TeamId = id, PlayerId = pid });

        await _db.SaveChangesAsync(ct);
        return await Get(id, ct);
    }

    [HttpDelete("{id:int}/roster/{playerId:int}")]
    public async Task<IActionResult> RemoveMember(int id, int playerId, CancellationToken ct)
    {
        var tp = await _db.TeamPlayers.FirstOrDefaultAsync(x => x.TeamId == id && x.PlayerId == playerId, ct);
        if (tp is null) return NotFound();
        _db.TeamPlayers.Remove(tp);
        await _db.SaveChangesAsync(ct);
        return NoContent();
    }

    /// <summary>Map of PlayerId → age-bracket name from each player's most-recent registration that
    /// has a classification assigned. Players with no classified registration are absent.</summary>
    private async Task<Dictionary<int, string>> AgeBracketByPlayerAsync(IReadOnlyList<int> playerIds, CancellationToken ct)
    {
        if (playerIds.Count == 0) return new();
        var rows = await _db.RegistrationPlayers
            .Where(rp => playerIds.Contains(rp.PlayerId) && rp.AgeClassificationId != null)
            .OrderByDescending(rp => rp.Registration!.CreatedAt)
            .Select(rp => new { rp.PlayerId, Bracket = rp.AgeClassification!.Name })
            .ToListAsync(ct);
        var map = new Dictionary<int, string>();
        foreach (var r in rows)
            map.TryAdd(r.PlayerId, r.Bracket);
        return map;
    }

    private async Task<RosterTeamSummary> SummarizeAsync(int teamId, CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        return await _db.Teams
            .Where(t => t.Id == teamId)
            .Include(t => t.MessageGroup)
            .Select(t => new RosterTeamSummary(
                t.Id, t.Name,
                t.Roster.Count,
                t.Games.Count(g => g.StartsAt >= now && !g.IsCancelled),
                t.GotSportEventId > 0 && t.GotSportTeamId > 0,
                t.MessageGroupId,
                t.MessageGroup != null ? t.MessageGroup.Name : null,
                t.CreatedAt))
            .FirstAsync(ct);
    }
}
