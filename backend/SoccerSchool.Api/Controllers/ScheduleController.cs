using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SoccerSchool.Api.Data;
using SoccerSchool.Api.Domain;
using SoccerSchool.Api.Dtos;
using SoccerSchool.Api.Services;

namespace SoccerSchool.Api.Controllers;

/// <summary>
/// Admin-only schedule data: teams whose games come from an external iCal feed (GotSport),
/// and the synced game rows that the messaging Compose tab uses to autofill template variables.
/// Distinct from MessagingController to keep that surface focused on the send pipeline.
/// </summary>
[ApiController]
[Route("api/schedule")]
[Authorize(Roles = Roles.Admin)]
public class ScheduleController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IScheduleSyncService _sync;

    public ScheduleController(AppDbContext db, IScheduleSyncService sync)
    {
        _db = db;
        _sync = sync;
    }

    [HttpGet("teams")]
    public async Task<ActionResult<IEnumerable<TeamSummary>>> ListTeams(CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var rows = await _db.Teams
            .Include(t => t.MessageGroup)
            .OrderBy(t => t.Name)
            .Select(t => new TeamSummary(
                t.Id, t.Name, t.GotSportEventId, t.GotSportTeamId, t.MessageGroupId,
                t.MessageGroup != null ? t.MessageGroup.Name : null,
                t.LastSyncedAt, t.LastSyncMessage,
                t.Games.Count(g => g.StartsAt >= now),
                t.CreatedAt))
            .ToListAsync(ct);
        return Ok(rows);
    }

    [HttpGet("teams/{id:int}")]
    public async Task<ActionResult<TeamDetail>> GetTeam(int id, CancellationToken ct)
    {
        var team = await _db.Teams
            .Include(t => t.MessageGroup)
            .Include(t => t.Games)
            .FirstOrDefaultAsync(t => t.Id == id, ct);
        if (team is null) return NotFound();

        var now = DateTime.UtcNow;
        var games = team.Games
            .Where(g => g.StartsAt >= now.AddDays(-1))
            .OrderBy(g => g.StartsAt)
            .Select(g => new ScheduledGameDto(
                g.Id, team.Id, team.Name, team.MessageGroupId, team.MessageGroup?.Name,
                g.StartsAt, g.EndsAt, g.Summary, g.Location, g.Description,
                g.OpponentName, g.IsHome))
            .ToList();

        return Ok(new TeamDetail(
            team.Id, team.Name, team.GotSportEventId, team.GotSportTeamId, team.MessageGroupId,
            team.MessageGroup?.Name, team.LastSyncedAt, team.LastSyncMessage,
            team.CreatedAt, games));
    }

    [HttpPost("teams")]
    public async Task<ActionResult<TeamSummary>> CreateTeam(
        [FromBody] SaveTeamRequest request, CancellationToken ct)
    {
        var (name, eventId, teamId, err) = ValidateAndResolveIds(request);
        if (err is not null) return BadRequest(err);
        if (await _db.Teams.AnyAsync(t => t.Name == name, ct))
            return Conflict($"A team named '{name}' already exists.");
        if (request.MessageGroupId is int gid && !await _db.MessageGroups.AnyAsync(g => g.Id == gid, ct))
            return BadRequest("Linked message group not found.");

        var team = new Team
        {
            Name = name!,
            GotSportEventId = eventId,
            GotSportTeamId = teamId,
            MessageGroupId = request.MessageGroupId
        };
        _db.Teams.Add(team);
        await _db.SaveChangesAsync(ct);
        return Ok(await SummarizeAsync(team.Id, ct));
    }

    [HttpPut("teams/{id:int}")]
    public async Task<ActionResult<TeamSummary>> UpdateTeam(
        int id, [FromBody] SaveTeamRequest request, CancellationToken ct)
    {
        var team = await _db.Teams.FindAsync(new object?[] { id }, ct);
        if (team is null) return NotFound();
        var (name, eventId, teamId, err) = ValidateAndResolveIds(request);
        if (err is not null) return BadRequest(err);
        if (await _db.Teams.AnyAsync(t => t.Name == name && t.Id != id, ct))
            return Conflict($"A team named '{name}' already exists.");
        if (request.MessageGroupId is int gid && !await _db.MessageGroups.AnyAsync(g => g.Id == gid, ct))
            return BadRequest("Linked message group not found.");

        team.Name = name!;
        team.GotSportEventId = eventId;
        team.GotSportTeamId = teamId;
        team.MessageGroupId = request.MessageGroupId;
        await _db.SaveChangesAsync(ct);
        return Ok(await SummarizeAsync(team.Id, ct));
    }

    /// <summary>Pulls GotSport event/team IDs from either the explicit fields or a pasted schedule URL.</summary>
    private static (string? Name, int EventId, int TeamId, string? Error) ValidateAndResolveIds(SaveTeamRequest request)
    {
        var name = request.Name?.Trim();
        if (string.IsNullOrWhiteSpace(name)) return (null, 0, 0, "Name is required.");

        int eventId = request.GotSportEventId ?? 0;
        int teamId = request.GotSportTeamId ?? 0;

        if ((eventId <= 0 || teamId <= 0) && !string.IsNullOrWhiteSpace(request.ScheduleUrl))
        {
            // Accept either the bare URL or one with a leading "https://"; extract event + team IDs.
            var url = request.ScheduleUrl.Trim();
            var em = Regex.Match(url, @"/events/(\d+)/schedules", RegexOptions.IgnoreCase);
            var tm = Regex.Match(url, @"[?&]team=(\d+)", RegexOptions.IgnoreCase);
            if (em.Success && int.TryParse(em.Groups[1].Value, out var e)) eventId = e;
            if (tm.Success && int.TryParse(tm.Groups[1].Value, out var t)) teamId = t;
        }

        if (eventId <= 0 || teamId <= 0)
            return (null, 0, 0, "GotSport event ID and team ID are required. Paste the schedule URL or enter them directly.");
        return (name, eventId, teamId, null);
    }

    [HttpDelete("teams/{id:int}")]
    public async Task<IActionResult> DeleteTeam(int id, CancellationToken ct)
    {
        var team = await _db.Teams.FindAsync(new object?[] { id }, ct);
        if (team is null) return NotFound();
        _db.Teams.Remove(team);
        await _db.SaveChangesAsync(ct);
        return NoContent();
    }

    [HttpPost("teams/{id:int}/sync")]
    public async Task<ActionResult<ScheduleSyncResultDto>> Sync(int id, CancellationToken ct)
    {
        var result = await _sync.SyncTeamAsync(id, ct);
        if (!result.Success) return UnprocessableEntity(new ScheduleSyncResultDto(false, 0, 0, result.Message));
        return Ok(new ScheduleSyncResultDto(true, result.Added, result.Updated, result.Message));
    }

    /// <summary>
    /// Upcoming games across all teams within the given window. Used by the Compose tab game
    /// picker to autofill template variables.
    /// </summary>
    [HttpGet("games")]
    public async Task<ActionResult<IEnumerable<ScheduledGameDto>>> ListGames(
        [FromQuery] int days = 14, CancellationToken ct = default)
    {
        if (days < 1) days = 1;
        if (days > 180) days = 180;
        var from = DateTime.UtcNow.AddHours(-2);
        var to = DateTime.UtcNow.AddDays(days);

        var games = await _db.ScheduledGames
            .Include(g => g.Team).ThenInclude(t => t!.MessageGroup)
            .Where(g => g.StartsAt >= from && g.StartsAt <= to)
            .OrderBy(g => g.StartsAt)
            .Select(g => new ScheduledGameDto(
                g.Id, g.TeamId, g.Team!.Name, g.Team.MessageGroupId, g.Team.MessageGroup != null ? g.Team.MessageGroup.Name : null,
                g.StartsAt, g.EndsAt, g.Summary, g.Location, g.Description,
                g.OpponentName, g.IsHome))
            .ToListAsync(ct);
        return Ok(games);
    }

    private async Task<TeamSummary> SummarizeAsync(int teamId, CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        return await _db.Teams
            .Where(t => t.Id == teamId)
            .Include(t => t.MessageGroup)
            .Select(t => new TeamSummary(
                t.Id, t.Name, t.GotSportEventId, t.GotSportTeamId, t.MessageGroupId,
                t.MessageGroup != null ? t.MessageGroup.Name : null,
                t.LastSyncedAt, t.LastSyncMessage,
                t.Games.Count(g => g.StartsAt >= now),
                t.CreatedAt))
            .FirstAsync(ct);
    }
}
