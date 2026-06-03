using System.Globalization;
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
    private readonly ITeamSnapSyncService _teamSnapSync;

    public ScheduleController(AppDbContext db, IScheduleSyncService sync, ITeamSnapSyncService teamSnapSync)
    {
        _db = db;
        _sync = sync;
        _teamSnapSync = teamSnapSync;
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
            .Include(t => t.Games).ThenInclude(g => g.Tournament)
            .FirstOrDefaultAsync(t => t.Id == id, ct);
        if (team is null) return NotFound();

        var now = DateTime.UtcNow;
        var games = team.Games
            .Where(g => g.StartsAt >= now.AddDays(-1))
            .OrderBy(g => g.StartsAt)
            .Select(g => new ScheduledGameDto(
                g.Id, team.Id, team.Name, team.MessageGroupId, team.MessageGroup?.Name,
                g.Kind, g.StartsAt, g.EndsAt, g.Summary, g.Location, g.Description,
                g.OpponentName, g.IsHome, g.SeriesId, g.IsCancelled, g.CancelledAt,
                g.TournamentId, g.Tournament?.Name))
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

    // --- Tournaments (admin-owned events, optional GotSport import) ---

    [HttpGet("tournaments")]
    public async Task<ActionResult<IEnumerable<TournamentSummary>>> ListTournaments(CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var rows = await _db.Tournaments
            .OrderByDescending(t => t.CreatedAt)
            .Select(t => new TournamentSummary(
                t.Id, t.Name, t.Kind, t.StartDate, t.EndDate, t.TotalCost, t.CostPerPlayer,
                t.TeamId, t.Team != null ? t.Team.Name : null,
                t.GotSportEventId, t.GotSportTeamId,
                t.LastSyncedAt, t.LastSyncMessage,
                t.Games.Count,
                t.Games.Count(g => g.StartsAt >= now && !g.IsCancelled),
                t.Team != null ? t.Team.Roster.Count : 0,
                t.CreatedAt,
                t.Teams.OrderBy(tt => tt.CreatedAt).Select(tt => new TournamentTeamDto(
                    tt.Id, tt.TournamentId, tt.TeamId, tt.Team!.Name,
                    tt.GotSportEventId, tt.GotSportTeamId,
                    tt.TeamSnapEventId, tt.TeamSnapDivisionId, tt.TeamSnapParticipantId,
                    tt.LastSyncedAt, tt.LastSyncMessage,
                    tt.Team.Roster.Count,
                    tt.Team.Games.Count(g => g.TournamentId == tt.TournamentId),
                    tt.CreatedAt)).ToList()))
            .ToListAsync(ct);
        return Ok(rows);
    }

    [HttpPost("tournaments")]
    public async Task<ActionResult<TournamentSummary>> CreateTournament(
        [FromBody] SaveTournamentRequest request, CancellationToken ct)
    {
        var name = request.Name?.Trim();
        if (string.IsNullOrWhiteSpace(name)) return BadRequest("Name is required.");
        if (request.TeamId is int teamId && !await _db.Teams.AnyAsync(t => t.Id == teamId, ct))
            return BadRequest("Team not found.");
        var (gsEventId, gsTeamId) = ResolveOptionalGotSportIds(request);

        var tournament = new Tournament
        {
            Name = name!,
            Kind = request.Kind,
            StartDate = request.StartDate,
            EndDate = request.EndDate,
            TotalCost = request.TotalCost,
            CostPerPlayer = request.CostPerPlayer,
            TeamId = request.TeamId,
            GotSportEventId = gsEventId,
            GotSportTeamId = gsTeamId,
        };
        _db.Tournaments.Add(tournament);
        await _db.SaveChangesAsync(ct);
        return Ok(await SummarizeTournamentAsync(tournament.Id, ct));
    }

    [HttpPut("tournaments/{id:int}")]
    public async Task<ActionResult<TournamentSummary>> UpdateTournament(
        int id, [FromBody] SaveTournamentRequest request, CancellationToken ct)
    {
        var tournament = await _db.Tournaments.FindAsync(new object?[] { id }, ct);
        if (tournament is null) return NotFound();
        var name = request.Name?.Trim();
        if (string.IsNullOrWhiteSpace(name)) return BadRequest("Name is required.");
        if (request.TeamId is int teamId && !await _db.Teams.AnyAsync(t => t.Id == teamId, ct))
            return BadRequest("Team not found.");
        var (gsEventId, gsTeamId) = ResolveOptionalGotSportIds(request);

        tournament.Name = name!;
        tournament.Kind = request.Kind;
        tournament.StartDate = request.StartDate;
        tournament.EndDate = request.EndDate;
        tournament.TotalCost = request.TotalCost;
        tournament.CostPerPlayer = request.CostPerPlayer;
        tournament.TeamId = request.TeamId;
        tournament.GotSportEventId = gsEventId;
        tournament.GotSportTeamId = gsTeamId;
        await _db.SaveChangesAsync(ct);
        return Ok(await SummarizeTournamentAsync(tournament.Id, ct));
    }

    [HttpDelete("tournaments/{id:int}")]
    public async Task<IActionResult> DeleteTournament(int id, CancellationToken ct)
    {
        var tournament = await _db.Tournaments.FindAsync(new object?[] { id }, ct);
        if (tournament is null) return NotFound();
        // Games keep existing as the team's games (FK is SetNull) — only the tournament link drops.
        _db.Tournaments.Remove(tournament);
        await _db.SaveChangesAsync(ct);
        return NoContent();
    }

    [HttpPost("tournaments/{id:int}/sync")]
    public async Task<ActionResult<ScheduleSyncResultDto>> SyncTournament(int id, CancellationToken ct)
    {
        var result = await _sync.SyncTournamentAsync(id, ct);
        if (!result.Success) return UnprocessableEntity(new ScheduleSyncResultDto(false, 0, 0, result.Message));
        return Ok(new ScheduleSyncResultDto(true, result.Added, result.Updated, result.Message));
    }

    /// <summary>Legacy: creates a brand-new Team and points <c>Tournament.TeamId</c> at it. Kept
    /// for backward compat — new code path uses the multi-team <c>TournamentTeams</c> join via
    /// <see cref="AddTournamentTeam"/>.</summary>
    [HttpPost("tournaments/{id:int}/team")]
    public async Task<ActionResult<TournamentSummary>> CreateTeamForTournament(
        int id, [FromBody] CreateTournamentTeamRequest request, CancellationToken ct)
    {
        var tournament = await _db.Tournaments.FindAsync(new object?[] { id }, ct);
        if (tournament is null) return NotFound();
        if (tournament.TeamId is not null) return Conflict("This tournament already has a team.");
        var name = request.Name?.Trim();
        if (string.IsNullOrWhiteSpace(name)) return BadRequest("Team name is required.");
        if (await _db.Teams.AnyAsync(t => t.Name == name, ct))
            return Conflict($"A team named '{name}' already exists.");

        var team = new Team { Name = name! };
        _db.Teams.Add(team);
        await _db.SaveChangesAsync(ct);
        tournament.TeamId = team.Id;
        await _db.SaveChangesAsync(ct);
        return Ok(await SummarizeTournamentAsync(tournament.Id, ct));
    }

    // --- Multi-team workflow ---

    /// <summary>Adds a team to a tournament. Either picks an existing team via
    /// <c>ExistingTeamId</c> or creates a fresh one via <c>NewTeamName</c>. Optional GotSport
    /// IDs (or a pasted schedule URL) wire up per-participation sync.</summary>
    [HttpPost("tournaments/{id:int}/teams")]
    public async Task<ActionResult<TournamentSummary>> AddTournamentTeam(
        int id, [FromBody] AddTournamentTeamRequest request, CancellationToken ct)
    {
        var tournament = await _db.Tournaments.FindAsync(new object?[] { id }, ct);
        if (tournament is null) return NotFound();

        int teamId;
        if (request.ExistingTeamId is int existingId)
        {
            if (!await _db.Teams.AnyAsync(t => t.Id == existingId, ct))
                return BadRequest("Team not found.");
            teamId = existingId;
        }
        else
        {
            var newName = request.NewTeamName?.Trim();
            if (string.IsNullOrWhiteSpace(newName))
                return BadRequest("Either pick an existing team or provide NewTeamName.");
            if (await _db.Teams.AnyAsync(t => t.Name == newName, ct))
                return Conflict($"A team named '{newName}' already exists.");
            var team = new Team { Name = newName! };
            _db.Teams.Add(team);
            await _db.SaveChangesAsync(ct);
            teamId = team.Id;
        }

        if (await _db.TournamentTeams.AnyAsync(tt => tt.TournamentId == id && tt.TeamId == teamId, ct))
            return Conflict("That team is already in this tournament.");

        var (gsEventId, gsTeamId) = ResolveOptionalGotSportTeamIds(request);
        var (tsEventId, tsDivisionId, tsParticipantId) = ResolveOptionalTeamSnapIds(request);

        _db.TournamentTeams.Add(new TournamentTeam
        {
            TournamentId = id,
            TeamId = teamId,
            GotSportEventId = gsEventId,
            GotSportTeamId = gsTeamId,
            TeamSnapEventId = tsEventId,
            TeamSnapDivisionId = tsDivisionId,
            TeamSnapParticipantId = tsParticipantId,
        });
        await _db.SaveChangesAsync(ct);
        return Ok(await SummarizeTournamentAsync(id, ct));
    }

    [HttpPut("tournaments/{id:int}/teams/{ttId:int}")]
    public async Task<ActionResult<TournamentSummary>> UpdateTournamentTeam(
        int id, int ttId, [FromBody] UpdateTournamentTeamRequest request, CancellationToken ct)
    {
        var tt = await _db.TournamentTeams.FirstOrDefaultAsync(x => x.Id == ttId && x.TournamentId == id, ct);
        if (tt is null) return NotFound();
        var (gsEventId, gsTeamId) = ResolveOptionalGotSportTeamIds(request);
        var (tsEventId, tsDivisionId, tsParticipantId) = ResolveOptionalTeamSnapIds(request);
        tt.GotSportEventId = gsEventId;
        tt.GotSportTeamId = gsTeamId;
        tt.TeamSnapEventId = tsEventId;
        tt.TeamSnapDivisionId = tsDivisionId;
        tt.TeamSnapParticipantId = tsParticipantId;
        await _db.SaveChangesAsync(ct);
        return Ok(await SummarizeTournamentAsync(id, ct));
    }

    /// <summary>Removes a team from this tournament. The underlying Team row stays — it might
    /// be reused for other tournaments or as a season team. Games for this team in this
    /// tournament keep existing (their TournamentId stays as a tag).</summary>
    [HttpDelete("tournaments/{id:int}/teams/{ttId:int}")]
    public async Task<ActionResult<TournamentSummary>> RemoveTournamentTeam(
        int id, int ttId, CancellationToken ct)
    {
        var tt = await _db.TournamentTeams.FirstOrDefaultAsync(x => x.Id == ttId && x.TournamentId == id, ct);
        if (tt is null) return NotFound();
        _db.TournamentTeams.Remove(tt);
        await _db.SaveChangesAsync(ct);
        return Ok(await SummarizeTournamentAsync(id, ct));
    }

    /// <summary>Syncs games for a single TournamentTeam participation. Dispatches to TeamSnap
    /// when (TeamSnapEventId + TeamSnapParticipantId) are both set on the row; otherwise falls
    /// back to the GotSport scraper using (GotSportEventId + GotSportTeamId). Games are upserted
    /// onto the underlying Team with TournamentId = this tournament.</summary>
    [HttpPost("tournaments/{id:int}/teams/{ttId:int}/sync")]
    public async Task<ActionResult<ScheduleSyncResultDto>> SyncTournamentTeam(
        int id, int ttId, CancellationToken ct)
    {
        var tt = await _db.TournamentTeams.AsNoTracking().FirstOrDefaultAsync(x => x.Id == ttId && x.TournamentId == id, ct);
        if (tt is null) return NotFound();
        var useTeamSnap = tt.TeamSnapEventId > 0 && tt.TeamSnapParticipantId > 0;
        var result = useTeamSnap
            ? await _teamSnapSync.SyncTournamentTeamAsync(ttId, ct)
            : await _sync.SyncTournamentTeamAsync(ttId, ct);
        if (!result.Success) return UnprocessableEntity(new ScheduleSyncResultDto(false, 0, 0, result.Message));
        return Ok(new ScheduleSyncResultDto(true, result.Added, result.Updated, result.Message));
    }

    /// <summary>Imports a schedule pasted out of the TeamSnap UI. Workaround for the public
    /// TeamSnap API not exposing per-match startDate/startTime/venueId — the admin pastes the
    /// visible schedule rows and we parse + upsert ScheduledGame rows for this team.</summary>
    [HttpPost("tournaments/{id:int}/teams/{ttId:int}/import-pasted-schedule")]
    public async Task<ActionResult<ScheduleSyncResultDto>> ImportPastedSchedule(
        int id, int ttId, [FromBody] ImportPastedScheduleRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Text))
            return BadRequest(new ScheduleSyncResultDto(false, 0, 0, "Paste the schedule rows first."));
        var result = await _teamSnapSync.ImportPastedScheduleAsync(ttId, request.Text, ct);
        if (!result.Success) return UnprocessableEntity(new ScheduleSyncResultDto(false, 0, 0, result.Message));
        return Ok(new ScheduleSyncResultDto(true, result.Added, result.Updated, result.Message));
    }

    /// <summary>Same parsing rules as the legacy tournament-level GotSport resolver but for the
    /// per-participation request shape (Add/Update).</summary>
    private static (int EventId, int TeamId) ResolveOptionalGotSportTeamIds(AddTournamentTeamRequest request)
    {
        return ResolveFromExplicitOrUrl(request.GotSportEventId, request.GotSportTeamId, request.ScheduleUrl);
    }
    private static (int EventId, int TeamId) ResolveOptionalGotSportTeamIds(UpdateTournamentTeamRequest request)
    {
        return ResolveFromExplicitOrUrl(request.GotSportEventId, request.GotSportTeamId, request.ScheduleUrl);
    }
    private static (int EventId, int TeamId) ResolveFromExplicitOrUrl(int? evt, int? team, string? scheduleUrl)
    {
        int eventId = evt ?? 0;
        int teamId = team ?? 0;
        if ((eventId <= 0 || teamId <= 0) && !string.IsNullOrWhiteSpace(scheduleUrl))
        {
            var url = scheduleUrl.Trim();
            var em = Regex.Match(url, @"/events/(\d+)/schedules", RegexOptions.IgnoreCase);
            var tm = Regex.Match(url, @"[?&]team=(\d+)", RegexOptions.IgnoreCase);
            if (em.Success && int.TryParse(em.Groups[1].Value, out var e)) eventId = e;
            if (tm.Success && int.TryParse(tm.Groups[1].Value, out var t)) teamId = t;
        }
        return (Math.Max(eventId, 0), Math.Max(teamId, 0));
    }

    /// <summary>Resolves the TeamSnap (Event, Division, Participant) triple for an Add/Update
    /// request. Explicit fields take precedence; if blank, falls back to parsing the same
    /// <c>ScheduleUrl</c> field the GotSport resolver uses — TeamSnap URLs look like
    /// <c>events.teamsnap.com/events/{eventId}/results/division/{divisionId}/team/{participantId}</c>
    /// (the exact path varies; we just sniff the three numeric segments by keyword).</summary>
    private static (int EventId, int DivisionId, int ParticipantId) ResolveOptionalTeamSnapIds(AddTournamentTeamRequest request)
        => ResolveTeamSnapFromExplicitOrUrl(request.TeamSnapEventId, request.TeamSnapDivisionId, request.TeamSnapParticipantId, request.ScheduleUrl);
    private static (int EventId, int DivisionId, int ParticipantId) ResolveOptionalTeamSnapIds(UpdateTournamentTeamRequest request)
        => ResolveTeamSnapFromExplicitOrUrl(request.TeamSnapEventId, request.TeamSnapDivisionId, request.TeamSnapParticipantId, request.ScheduleUrl);
    private static (int EventId, int DivisionId, int ParticipantId) ResolveTeamSnapFromExplicitOrUrl(
        int? evt, int? division, int? participant, string? scheduleUrl)
    {
        int eventId = evt ?? 0;
        int divisionId = division ?? 0;
        int participantId = participant ?? 0;
        if ((eventId <= 0 || divisionId <= 0 || participantId <= 0)
            && !string.IsNullOrWhiteSpace(scheduleUrl)
            && scheduleUrl.Contains("teamsnap.com", StringComparison.OrdinalIgnoreCase))
        {
            var url = scheduleUrl.Trim();
            var em = Regex.Match(url, @"/events/(\d+)", RegexOptions.IgnoreCase);
            var dm = Regex.Match(url, @"/division/(\d+)", RegexOptions.IgnoreCase);
            var pm = Regex.Match(url, @"/(?:team|participant)/(\d+)", RegexOptions.IgnoreCase);
            if (em.Success && int.TryParse(em.Groups[1].Value, out var e)) eventId = e;
            if (dm.Success && int.TryParse(dm.Groups[1].Value, out var d)) divisionId = d;
            if (pm.Success && int.TryParse(pm.Groups[1].Value, out var p)) participantId = p;
        }
        return (Math.Max(eventId, 0), Math.Max(divisionId, 0), Math.Max(participantId, 0));
    }

    /// <summary>Picks GotSport IDs from the request — either explicit fields or parsed out of
    /// a pasted schedule URL. New-style tournaments leave both at 0 (no GotSport sync).</summary>
    private static (int EventId, int TeamId) ResolveOptionalGotSportIds(SaveTournamentRequest request)
    {
        int eventId = request.GotSportEventId ?? 0;
        int teamId = request.GotSportTeamId ?? 0;
        if ((eventId <= 0 || teamId <= 0) && !string.IsNullOrWhiteSpace(request.ScheduleUrl))
        {
            var url = request.ScheduleUrl.Trim();
            var em = Regex.Match(url, @"/events/(\d+)/schedules", RegexOptions.IgnoreCase);
            var tm = Regex.Match(url, @"[?&]team=(\d+)", RegexOptions.IgnoreCase);
            if (em.Success && int.TryParse(em.Groups[1].Value, out var e)) eventId = e;
            if (tm.Success && int.TryParse(tm.Groups[1].Value, out var t)) teamId = t;
        }
        return (Math.Max(eventId, 0), Math.Max(teamId, 0));
    }

    private async Task<TournamentSummary> SummarizeTournamentAsync(int id, CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        return await _db.Tournaments
            .Where(t => t.Id == id)
            .Select(t => new TournamentSummary(
                t.Id, t.Name, t.Kind, t.StartDate, t.EndDate, t.TotalCost, t.CostPerPlayer,
                t.TeamId, t.Team != null ? t.Team.Name : null,
                t.GotSportEventId, t.GotSportTeamId,
                t.LastSyncedAt, t.LastSyncMessage,
                t.Games.Count,
                t.Games.Count(g => g.StartsAt >= now && !g.IsCancelled),
                t.Team != null ? t.Team.Roster.Count : 0,
                t.CreatedAt,
                t.Teams.OrderBy(tt => tt.CreatedAt).Select(tt => new TournamentTeamDto(
                    tt.Id, tt.TournamentId, tt.TeamId, tt.Team!.Name,
                    tt.GotSportEventId, tt.GotSportTeamId,
                    tt.TeamSnapEventId, tt.TeamSnapDivisionId, tt.TeamSnapParticipantId,
                    tt.LastSyncedAt, tt.LastSyncMessage,
                    tt.Team.Roster.Count,
                    tt.Team.Games.Count(g => g.TournamentId == tt.TournamentId),
                    tt.CreatedAt)).ToList()))
            .FirstAsync(ct);
    }

    // --- Manual practice CRUD ---
    //
    // Practices are admin-entered (date + location) rather than scraped — GotSport doesn't list
    // practices. They live in the same ScheduledGames table as games with Kind=Practice and a
    // synthesized ExternalUid (`practice-{guid}`) so the unique (team, uid) index doesn't trip.

    [HttpPost("teams/{teamId:int}/practices")]
    public async Task<ActionResult<ScheduledGameDto>> CreatePractice(
        int teamId, [FromBody] SavePracticeRequest request, CancellationToken ct)
    {
        var team = await _db.Teams.Include(t => t.MessageGroup).FirstOrDefaultAsync(t => t.Id == teamId, ct);
        if (team is null) return NotFound();
        if (request.StartsAt == default) return BadRequest("Start time is required.");

        var practice = new ScheduledGame
        {
            TeamId = team.Id,
            Kind = ScheduledEventKind.Practice,
            ExternalUid = $"practice-{Guid.NewGuid():N}",
            StartsAt = DateTime.SpecifyKind(request.StartsAt, DateTimeKind.Utc),
            EndsAt = request.EndsAt.HasValue ? DateTime.SpecifyKind(request.EndsAt.Value, DateTimeKind.Utc) : null,
            Location = string.IsNullOrWhiteSpace(request.Location) ? null : request.Location.Trim(),
            Summary = string.IsNullOrWhiteSpace(request.Summary) ? "Practice" : request.Summary.Trim(),
            CreatedAt = DateTime.UtcNow,
            LastSeenAt = DateTime.UtcNow
        };
        _db.ScheduledGames.Add(practice);
        await _db.SaveChangesAsync(ct);
        return Ok(ToDto(practice, team));
    }

    /// <summary>
    /// Materialize a recurring practice series. For each date in [StartDate, EndDate] whose
    /// day-of-week appears in DaysOfWeek, creates one ScheduledGame at StartTime (local).
    /// All occurrences share a single SeriesId so the UI can show "(series)" badges and so a
    /// future "cancel entire series" action can target them as a group.
    /// </summary>
    [HttpPost("teams/{teamId:int}/practice-series")]
    public async Task<ActionResult<PracticeSeriesCreatedDto>> CreatePracticeSeries(
        int teamId, [FromBody] SavePracticeSeriesRequest request, CancellationToken ct)
    {
        var team = await _db.Teams.Include(t => t.MessageGroup).FirstOrDefaultAsync(t => t.Id == teamId, ct);
        if (team is null) return NotFound();
        if (request.StartDate == default || request.EndDate == default)
            return BadRequest("StartDate and EndDate are required.");
        if (request.EndDate.Date < request.StartDate.Date)
            return BadRequest("EndDate must be on or after StartDate.");
        if (request.DaysOfWeek is null || request.DaysOfWeek.Length == 0)
            return BadRequest("Pick at least one day of the week.");
        if (!TryParseTime(request.StartTime, out var startTime))
            return BadRequest("StartTime must be HH:mm (e.g. \"17:00\").");
        TimeSpan? endTime = null;
        if (!string.IsNullOrWhiteSpace(request.EndTime))
        {
            if (!TryParseTime(request.EndTime, out var et)) return BadRequest("EndTime must be HH:mm.");
            endTime = et;
        }

        // Soft cap so an admin who fat-fingers a 10-year range doesn't insert 3,000 rows.
        var span = (request.EndDate.Date - request.StartDate.Date).Days + 1;
        if (span > 365) return BadRequest("Series spans more than 365 days; trim the date range.");

        var dows = new HashSet<DayOfWeek>(request.DaysOfWeek.Select(d => (DayOfWeek)d));
        var seriesId = Guid.NewGuid();
        var now = DateTime.UtcNow;
        var added = new List<ScheduledGame>();

        // Series times are entered as wall-clock Pacific (DST-aware). Convert each occurrence's
        // local datetime to UTC explicitly — `SpecifyKind(..., Utc)` alone would mis-label the
        // local time as UTC and shift it by 7-8 hours.
        var pacific = ResolvePacificTimeZone();

        for (var date = request.StartDate.Date; date <= request.EndDate.Date; date = date.AddDays(1))
        {
            if (!dows.Contains(date.DayOfWeek)) continue;
            var startsAtLocal = DateTime.SpecifyKind(date + startTime, DateTimeKind.Unspecified);
            var endsAtLocal = endTime.HasValue
                ? (DateTime?)DateTime.SpecifyKind(date + endTime.Value, DateTimeKind.Unspecified)
                : null;
            var practice = new ScheduledGame
            {
                TeamId = team.Id,
                Kind = ScheduledEventKind.Practice,
                SeriesId = seriesId,
                ExternalUid = $"practice-{seriesId:N}-{date:yyyyMMdd}",
                StartsAt = TimeZoneInfo.ConvertTimeToUtc(startsAtLocal, pacific),
                EndsAt = endsAtLocal.HasValue ? TimeZoneInfo.ConvertTimeToUtc(endsAtLocal.Value, pacific) : null,
                Location = string.IsNullOrWhiteSpace(request.Location) ? null : request.Location.Trim(),
                Summary = string.IsNullOrWhiteSpace(request.Summary) ? "Practice" : request.Summary.Trim(),
                CreatedAt = now,
                LastSeenAt = now
            };
            _db.ScheduledGames.Add(practice);
            added.Add(practice);
        }
        await _db.SaveChangesAsync(ct);

        return Ok(new PracticeSeriesCreatedDto(
            seriesId,
            added.Count,
            added.Select(p => ToDto(p, team)).ToList()));
    }

    private static bool TryParseTime(string text, out TimeSpan time)
    {
        // Accept "HH:mm" or "H:mm" 24-hour.
        return TimeSpan.TryParseExact(text, new[] { @"hh\:mm", @"h\:mm" }, CultureInfo.InvariantCulture, out time);
    }

    /// <summary>America/Los_Angeles, with Windows-name fallback. LVSS operates in Las Vegas which
    /// shares Pacific Time, including DST.</summary>
    private static TimeZoneInfo ResolvePacificTimeZone()
    {
        try { return TimeZoneInfo.FindSystemTimeZoneById("America/Los_Angeles"); }
        catch { return TimeZoneInfo.FindSystemTimeZoneById("Pacific Standard Time"); }
    }

    [HttpPut("practices/{id:int}")]
    public async Task<ActionResult<ScheduledGameDto>> UpdatePractice(
        int id, [FromBody] SavePracticeRequest request, CancellationToken ct)
    {
        var practice = await _db.ScheduledGames
            .Include(g => g.Team).ThenInclude(t => t!.MessageGroup)
            .FirstOrDefaultAsync(g => g.Id == id && g.Kind == ScheduledEventKind.Practice, ct);
        if (practice is null) return NotFound();
        if (request.StartsAt == default) return BadRequest("Start time is required.");

        practice.StartsAt = DateTime.SpecifyKind(request.StartsAt, DateTimeKind.Utc);
        practice.EndsAt = request.EndsAt.HasValue ? DateTime.SpecifyKind(request.EndsAt.Value, DateTimeKind.Utc) : null;
        practice.Location = string.IsNullOrWhiteSpace(request.Location) ? null : request.Location.Trim();
        practice.Summary = string.IsNullOrWhiteSpace(request.Summary) ? "Practice" : request.Summary.Trim();
        practice.LastSeenAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        return Ok(ToDto(practice, practice.Team!));
    }

    [HttpDelete("practices/{id:int}")]
    public async Task<IActionResult> DeletePractice(int id, CancellationToken ct)
    {
        var practice = await _db.ScheduledGames
            .FirstOrDefaultAsync(g => g.Id == id && g.Kind == ScheduledEventKind.Practice, ct);
        if (practice is null) return NotFound();
        _db.ScheduledGames.Remove(practice);
        await _db.SaveChangesAsync(ct);
        return NoContent();
    }

    /// <summary>Mark a practice as cancelled. The row stays in the DB so historical broadcasts
    /// that linked to it remain readable and so the cancellation-notification flow can look up
    /// who previously got the reminder. Hides it from the event picker; UI renders it muted.</summary>
    [HttpPost("practices/{id:int}/cancel")]
    public Task<ActionResult<ScheduledGameDto>> CancelPractice(int id, CancellationToken ct) =>
        CancelEventInternal(id, ScheduledEventKind.Practice, ct);

    // --- Manual miscellaneous CRUD ---
    //
    // One-off team events that aren't games or practices (banquets, team photos, fundraisers).
    // Same shape as practice rows (date + location + summary), just Kind=Miscellaneous so they
    // surface in their own tab and don't get confused with weekly practice scheduling.

    [HttpPost("teams/{teamId:int}/misc-events")]
    public async Task<ActionResult<ScheduledGameDto>> CreateMiscEvent(
        int teamId, [FromBody] SavePracticeRequest request, CancellationToken ct)
    {
        var team = await _db.Teams.Include(t => t.MessageGroup).FirstOrDefaultAsync(t => t.Id == teamId, ct);
        if (team is null) return NotFound();
        if (request.StartsAt == default) return BadRequest("Start time is required.");

        var ev = new ScheduledGame
        {
            TeamId = team.Id,
            Kind = ScheduledEventKind.Miscellaneous,
            ExternalUid = $"misc-{Guid.NewGuid():N}",
            StartsAt = DateTime.SpecifyKind(request.StartsAt, DateTimeKind.Utc),
            EndsAt = request.EndsAt.HasValue ? DateTime.SpecifyKind(request.EndsAt.Value, DateTimeKind.Utc) : null,
            Location = string.IsNullOrWhiteSpace(request.Location) ? null : request.Location.Trim(),
            Summary = string.IsNullOrWhiteSpace(request.Summary) ? "Event" : request.Summary.Trim(),
            CreatedAt = DateTime.UtcNow,
            LastSeenAt = DateTime.UtcNow
        };
        _db.ScheduledGames.Add(ev);
        await _db.SaveChangesAsync(ct);
        return Ok(ToDto(ev, team));
    }

    [HttpPut("misc-events/{id:int}")]
    public async Task<ActionResult<ScheduledGameDto>> UpdateMiscEvent(
        int id, [FromBody] SavePracticeRequest request, CancellationToken ct)
    {
        var ev = await _db.ScheduledGames
            .Include(g => g.Team).ThenInclude(t => t!.MessageGroup)
            .FirstOrDefaultAsync(g => g.Id == id && g.Kind == ScheduledEventKind.Miscellaneous, ct);
        if (ev is null) return NotFound();
        if (request.StartsAt == default) return BadRequest("Start time is required.");

        ev.StartsAt = DateTime.SpecifyKind(request.StartsAt, DateTimeKind.Utc);
        ev.EndsAt = request.EndsAt.HasValue ? DateTime.SpecifyKind(request.EndsAt.Value, DateTimeKind.Utc) : null;
        ev.Location = string.IsNullOrWhiteSpace(request.Location) ? null : request.Location.Trim();
        ev.Summary = string.IsNullOrWhiteSpace(request.Summary) ? "Event" : request.Summary.Trim();
        ev.LastSeenAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        return Ok(ToDto(ev, ev.Team!));
    }

    [HttpDelete("misc-events/{id:int}")]
    public async Task<IActionResult> DeleteMiscEvent(int id, CancellationToken ct)
    {
        var ev = await _db.ScheduledGames
            .FirstOrDefaultAsync(g => g.Id == id && g.Kind == ScheduledEventKind.Miscellaneous, ct);
        if (ev is null) return NotFound();
        _db.ScheduledGames.Remove(ev);
        await _db.SaveChangesAsync(ct);
        return NoContent();
    }

    [HttpPost("misc-events/{id:int}/cancel")]
    public Task<ActionResult<ScheduledGameDto>> CancelMiscEvent(int id, CancellationToken ct) =>
        CancelEventInternal(id, ScheduledEventKind.Miscellaneous, ct);

    // --- Manual game CRUD ---
    //
    // Mirrors the practice endpoints. Manual games coexist with GotSport-scraped games in the
    // same table (Kind=Game). They get a synthesized ExternalUid (`manual-game-{guid}`) so the
    // unique (team, uid) index doesn't collide with scrape upserts.

    [HttpPost("teams/{teamId:int}/games")]
    public async Task<ActionResult<ScheduledGameDto>> CreateGame(
        int teamId, [FromBody] SaveGameRequest request, CancellationToken ct)
    {
        var team = await _db.Teams.Include(t => t.MessageGroup).FirstOrDefaultAsync(t => t.Id == teamId, ct);
        if (team is null) return NotFound();
        if (request.StartsAt == default) return BadRequest("Start time is required.");

        // If tied to a tournament, it must belong to this team.
        if (request.TournamentId is int tid &&
            !await _db.Tournaments.AnyAsync(t => t.Id == tid && t.TeamId == teamId, ct))
            return BadRequest("Tournament not found for this team.");

        var game = new ScheduledGame
        {
            TeamId = team.Id,
            TournamentId = request.TournamentId,
            Kind = ScheduledEventKind.Game,
            ExternalUid = $"manual-game-{Guid.NewGuid():N}",
            StartsAt = DateTime.SpecifyKind(request.StartsAt, DateTimeKind.Utc),
            EndsAt = request.EndsAt.HasValue ? DateTime.SpecifyKind(request.EndsAt.Value, DateTimeKind.Utc) : null,
            OpponentName = string.IsNullOrWhiteSpace(request.OpponentName) ? null : request.OpponentName.Trim(),
            IsHome = request.IsHome,
            Location = string.IsNullOrWhiteSpace(request.Location) ? null : request.Location.Trim(),
            Summary = string.IsNullOrWhiteSpace(request.Summary)
                ? (string.IsNullOrWhiteSpace(request.OpponentName) ? "Game" : $"vs {request.OpponentName.Trim()}")
                : request.Summary.Trim(),
            CreatedAt = DateTime.UtcNow,
            LastSeenAt = DateTime.UtcNow
        };
        _db.ScheduledGames.Add(game);
        await _db.SaveChangesAsync(ct);
        return Ok(ToDto(game, team));
    }

    [HttpPut("games/{id:int}")]
    public async Task<ActionResult<ScheduledGameDto>> UpdateGame(
        int id, [FromBody] SaveGameRequest request, CancellationToken ct)
    {
        var game = await _db.ScheduledGames
            .Include(g => g.Team).ThenInclude(t => t!.MessageGroup)
            .FirstOrDefaultAsync(g => g.Id == id && g.Kind == ScheduledEventKind.Game, ct);
        if (game is null) return NotFound();
        if (request.StartsAt == default) return BadRequest("Start time is required.");

        game.StartsAt = DateTime.SpecifyKind(request.StartsAt, DateTimeKind.Utc);
        game.EndsAt = request.EndsAt.HasValue ? DateTime.SpecifyKind(request.EndsAt.Value, DateTimeKind.Utc) : null;
        game.OpponentName = string.IsNullOrWhiteSpace(request.OpponentName) ? null : request.OpponentName.Trim();
        game.IsHome = request.IsHome;
        game.Location = string.IsNullOrWhiteSpace(request.Location) ? null : request.Location.Trim();
        game.Summary = string.IsNullOrWhiteSpace(request.Summary)
            ? (string.IsNullOrWhiteSpace(request.OpponentName) ? "Game" : $"vs {request.OpponentName.Trim()}")
            : request.Summary.Trim();
        game.LastSeenAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        return Ok(ToDto(game, game.Team!));
    }

    [HttpDelete("games/{id:int}")]
    public async Task<IActionResult> DeleteGame(int id, CancellationToken ct)
    {
        // Note: deleting a scraped GotSport game will re-appear on the next sync (upsert by UID).
        // Manual games (manual-game-* UID) stay deleted.
        var game = await _db.ScheduledGames
            .FirstOrDefaultAsync(g => g.Id == id && g.Kind == ScheduledEventKind.Game, ct);
        if (game is null) return NotFound();
        _db.ScheduledGames.Remove(game);
        await _db.SaveChangesAsync(ct);
        return NoContent();
    }

    [HttpPost("games/{id:int}/cancel")]
    public Task<ActionResult<ScheduledGameDto>> CancelGame(int id, CancellationToken ct) =>
        CancelEventInternal(id, ScheduledEventKind.Game, ct);

    private async Task<ActionResult<ScheduledGameDto>> CancelEventInternal(
        int id, ScheduledEventKind kind, CancellationToken ct)
    {
        var ev = await _db.ScheduledGames
            .Include(g => g.Team).ThenInclude(t => t!.MessageGroup)
            .FirstOrDefaultAsync(g => g.Id == id && g.Kind == kind, ct);
        if (ev is null) return NotFound();
        if (!ev.IsCancelled)
        {
            ev.IsCancelled = true;
            ev.CancelledAt = DateTime.UtcNow;
            await _db.SaveChangesAsync(ct);
        }
        return Ok(ToDto(ev, ev.Team!));
    }

    /// <summary>Returns the distinct recipients (phone + name + language) of every prior broadcast
    /// that announced this event. Drives the cancellation-notification flow: admin sees this list
    /// and the Compose tab pre-populates with them as an ad-hoc list.</summary>
    [HttpGet("events/{id:int}/broadcast-recipients")]
    public async Task<ActionResult<IEnumerable<EventRecipientDto>>> GetEventBroadcastRecipients(int id, CancellationToken ct)
    {
        var exists = await _db.ScheduledGames.AnyAsync(g => g.Id == id, ct);
        if (!exists) return NotFound();
        // Dedupe by phone, preferring the most recent broadcast's language for the recipient
        // (parents who got the most recent reminder are the most relevant audience).
        var rows = await _db.BroadcastRecipients
            .Where(r => r.Broadcast!.ScheduledGameId == id)
            .OrderByDescending(r => r.Broadcast!.CreatedAt)
            .Select(r => new { r.Phone, r.Name, r.Language })
            .ToListAsync(ct);
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var result = new List<EventRecipientDto>();
        foreach (var r in rows)
        {
            if (string.IsNullOrWhiteSpace(r.Phone)) continue;
            if (!seen.Add(r.Phone)) continue;
            result.Add(new EventRecipientDto(r.Phone, r.Name, r.Language));
        }
        return Ok(result);
    }

    // --- Event attendance (per rostered player confirmation) ---

    /// <summary>The event's team roster with each player's confirmation status (Pending when no row
    /// exists yet) plus headline counts. Drives the "click an event → confirm players" panel.</summary>
    [HttpGet("events/{eventId:int}/attendance")]
    public async Task<ActionResult<EventAttendanceListDto>> GetAttendance(int eventId, CancellationToken ct)
    {
        var teamId = await _db.ScheduledGames
            .Where(g => g.Id == eventId)
            .Select(g => (int?)g.TeamId)
            .FirstOrDefaultAsync(ct);
        if (teamId is null) return NotFound();

        var roster = await _db.TeamPlayers
            .Where(tp => tp.TeamId == teamId)
            .Select(tp => new
            {
                tp.PlayerId,
                tp.Player!.FirstName,
                tp.Player.LastName,
                ParentFirst = tp.Player.ParentAccount!.FirstName,
                ParentLast = tp.Player.ParentAccount.LastName,
                ParentPhone = tp.Player.ParentAccount.CellPhone,
            })
            .ToListAsync(ct);

        var attendance = await _db.EventAttendances
            .Where(a => a.ScheduledGameId == eventId)
            .ToDictionaryAsync(a => a.PlayerId, a => a, ct);

        var items = roster
            .Select(r =>
            {
                attendance.TryGetValue(r.PlayerId, out var a);
                var parentName = $"{r.ParentFirst} {r.ParentLast}".Trim();
                return new EventAttendanceDto(
                    r.PlayerId, r.FirstName, r.LastName,
                    string.IsNullOrWhiteSpace(parentName) ? null : parentName,
                    r.ParentPhone,
                    a?.Status ?? AttendanceStatus.Pending,
                    a?.Source ?? AttendanceSource.ParentReply,
                    a?.UpdatedAt);
            })
            .OrderBy(i => i.LastName).ThenBy(i => i.FirstName)
            .ToList();

        return Ok(new EventAttendanceListDto(
            eventId,
            items.Count(i => i.Status == AttendanceStatus.Confirmed),
            items.Count(i => i.Status == AttendanceStatus.Declined),
            items.Count(i => i.Status == AttendanceStatus.Maybe),
            items.Count(i => i.Status == AttendanceStatus.Pending),
            items));
    }

    /// <summary>Admin manually sets a rostered player's status for the event. Stamped Source=Admin
    /// so a later parent reply won't overwrite the deliberate call.</summary>
    [HttpPut("events/{eventId:int}/attendance/{playerId:int}")]
    public async Task<ActionResult<EventAttendanceListDto>> SetAttendance(
        int eventId, int playerId, [FromBody] SetAttendanceRequest request, CancellationToken ct)
    {
        var ev = await _db.ScheduledGames.FirstOrDefaultAsync(g => g.Id == eventId, ct);
        if (ev is null) return NotFound();
        var onRoster = await _db.TeamPlayers.AnyAsync(tp => tp.TeamId == ev.TeamId && tp.PlayerId == playerId, ct);
        if (!onRoster) return BadRequest("Player is not on this team's roster.");

        var row = await _db.EventAttendances
            .FirstOrDefaultAsync(a => a.ScheduledGameId == eventId && a.PlayerId == playerId, ct);
        if (row is null)
        {
            row = new EventAttendance { ScheduledGameId = eventId, PlayerId = playerId };
            _db.EventAttendances.Add(row);
        }
        row.Status = request.Status;
        row.Source = AttendanceSource.Admin;
        row.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        return await GetAttendance(eventId, ct);
    }

    /// <summary>Per-event confirmation counts for a team's upcoming events — drives the schedule
    /// row badges + the re-send button's enabled state. Pending = roster minus those who answered.</summary>
    [HttpGet("teams/{teamId:int}/attendance-summary")]
    public async Task<ActionResult<IEnumerable<EventAttendanceSummaryDto>>> AttendanceSummary(int teamId, CancellationToken ct)
    {
        if (!await _db.Teams.AnyAsync(t => t.Id == teamId, ct)) return NotFound();

        var rosterCount = await _db.TeamPlayers.CountAsync(tp => tp.TeamId == teamId, ct);
        var now = DateTime.UtcNow;
        var eventIds = await _db.ScheduledGames
            .Where(g => g.TeamId == teamId && !g.IsCancelled && g.StartsAt >= now.AddDays(-1))
            .Select(g => g.Id)
            .ToListAsync(ct);
        if (eventIds.Count == 0) return Ok(Array.Empty<EventAttendanceSummaryDto>());

        var rows = await _db.EventAttendances
            .Where(a => eventIds.Contains(a.ScheduledGameId))
            .Select(a => new { a.ScheduledGameId, a.Status })
            .ToListAsync(ct);
        var byEvent = rows.GroupBy(a => a.ScheduledGameId).ToDictionary(g => g.Key, g => g.ToList());

        var result = eventIds.Select(id =>
        {
            byEvent.TryGetValue(id, out var rs);
            var confirmed = rs?.Count(r => r.Status == AttendanceStatus.Confirmed) ?? 0;
            var declined = rs?.Count(r => r.Status == AttendanceStatus.Declined) ?? 0;
            var maybe = rs?.Count(r => r.Status == AttendanceStatus.Maybe) ?? 0;
            var pending = Math.Max(0, rosterCount - (confirmed + declined + maybe));
            return new EventAttendanceSummaryDto(id, confirmed, declined, maybe, pending);
        }).ToList();
        return Ok(result);
    }

    // --- Tournament attendance (mirror of event attendance, scoped by tournament) ---

    /// <summary>Roster of the tournament's team with each player's confirmation status. Players
    /// with no row default to <see cref="AttendanceStatus.Pending"/>. Returned with the same
    /// headline counts the event-attendance UI uses.</summary>
    [HttpGet("tournaments/{tournamentId:int}/attendance")]
    public async Task<ActionResult<TournamentAttendanceListDto>> GetTournamentAttendance(int tournamentId, CancellationToken ct)
    {
        var tournament = await _db.Tournaments
            .Include(t => t.Team).ThenInclude(team => team!.Roster).ThenInclude(tp => tp.Player).ThenInclude(p => p!.ParentAccount)
            .FirstOrDefaultAsync(t => t.Id == tournamentId, ct);
        if (tournament is null) return NotFound();
        if (tournament.Team is null)
            return Ok(new TournamentAttendanceListDto(tournamentId, 0, 0, 0, 0, new List<TournamentAttendanceDto>()));

        var attendance = await _db.TournamentAttendances
            .Where(a => a.TournamentId == tournamentId)
            .ToDictionaryAsync(a => a.PlayerId, a => a, ct);

        var items = tournament.Team.Roster
            .Where(tp => tp.Player is not null)
            .Select(tp =>
            {
                attendance.TryGetValue(tp.PlayerId, out var a);
                var parent = tp.Player!.ParentAccount;
                var parentName = parent is null ? null : $"{parent.FirstName} {parent.LastName}".Trim();
                return new TournamentAttendanceDto(
                    tp.PlayerId, tp.Player.FirstName, tp.Player.LastName,
                    string.IsNullOrWhiteSpace(parentName) ? null : parentName,
                    parent?.CellPhone,
                    a?.Status ?? AttendanceStatus.Pending,
                    a?.Source ?? AttendanceSource.ParentReply,
                    a?.UpdatedAt);
            })
            .OrderBy(i => i.LastName).ThenBy(i => i.FirstName)
            .ToList();

        return Ok(new TournamentAttendanceListDto(
            tournamentId,
            items.Count(i => i.Status == AttendanceStatus.Confirmed),
            items.Count(i => i.Status == AttendanceStatus.Declined),
            items.Count(i => i.Status == AttendanceStatus.Maybe),
            items.Count(i => i.Status == AttendanceStatus.Pending),
            items));
    }

    [HttpPut("tournaments/{tournamentId:int}/attendance/{playerId:int}")]
    public async Task<ActionResult<TournamentAttendanceListDto>> SetTournamentAttendance(
        int tournamentId, int playerId, [FromBody] SetAttendanceRequest request, CancellationToken ct)
    {
        var tournament = await _db.Tournaments.FirstOrDefaultAsync(t => t.Id == tournamentId, ct);
        if (tournament is null) return NotFound();
        // Allow the set when the player is on ANY team in this tournament (legacy Tournament.TeamId
        // or any TournamentTeam row). Attendance rows are per (tournament, player), regardless of
        // which of the tournament's teams the player is on.
        var teamIds = await _db.TournamentTeams
            .Where(tt => tt.TournamentId == tournamentId)
            .Select(tt => tt.TeamId)
            .ToListAsync(ct);
        if (tournament.TeamId is int legacyId) teamIds.Add(legacyId);
        var onRoster = await _db.TeamPlayers.AnyAsync(tp => teamIds.Contains(tp.TeamId) && tp.PlayerId == playerId, ct);
        if (!onRoster) return BadRequest("Player is not on any roster in this tournament.");

        var row = await _db.TournamentAttendances
            .FirstOrDefaultAsync(a => a.TournamentId == tournamentId && a.PlayerId == playerId, ct);
        if (row is null)
        {
            row = new TournamentAttendance { TournamentId = tournamentId, PlayerId = playerId };
            _db.TournamentAttendances.Add(row);
        }
        row.Status = request.Status;
        row.Source = AttendanceSource.Admin;
        row.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        return await GetTournamentAttendance(tournamentId, ct);
    }

    /// <summary>Attendance for one team's roster within a tournament. The attendance rows are
    /// per (tournament, player), but the listing is scoped to the picked team so each tournament-
    /// team tab can render its own panel without showing players from other teams.</summary>
    [HttpGet("tournaments/{tournamentId:int}/teams/{teamId:int}/attendance")]
    public async Task<ActionResult<TournamentAttendanceListDto>> GetTournamentTeamAttendance(
        int tournamentId, int teamId, CancellationToken ct)
    {
        var tournamentExists = await _db.Tournaments.AnyAsync(t => t.Id == tournamentId, ct);
        if (!tournamentExists) return NotFound();
        var team = await _db.Teams
            .Include(t => t.Roster).ThenInclude(tp => tp.Player).ThenInclude(p => p!.ParentAccount)
            .FirstOrDefaultAsync(t => t.Id == teamId, ct);
        if (team is null) return NotFound();

        var attendance = await _db.TournamentAttendances
            .Where(a => a.TournamentId == tournamentId)
            .ToDictionaryAsync(a => a.PlayerId, a => a, ct);

        var items = team.Roster
            .Where(tp => tp.Player is not null)
            .Select(tp =>
            {
                attendance.TryGetValue(tp.PlayerId, out var a);
                var parent = tp.Player!.ParentAccount;
                var parentName = parent is null ? null : $"{parent.FirstName} {parent.LastName}".Trim();
                return new TournamentAttendanceDto(
                    tp.PlayerId, tp.Player.FirstName, tp.Player.LastName,
                    string.IsNullOrWhiteSpace(parentName) ? null : parentName,
                    parent?.CellPhone,
                    a?.Status ?? AttendanceStatus.Pending,
                    a?.Source ?? AttendanceSource.ParentReply,
                    a?.UpdatedAt);
            })
            .OrderBy(i => i.LastName).ThenBy(i => i.FirstName)
            .ToList();

        return Ok(new TournamentAttendanceListDto(
            tournamentId,
            items.Count(i => i.Status == AttendanceStatus.Confirmed),
            items.Count(i => i.Status == AttendanceStatus.Declined),
            items.Count(i => i.Status == AttendanceStatus.Maybe),
            items.Count(i => i.Status == AttendanceStatus.Pending),
            items));
    }

    private static ScheduledGameDto ToDto(ScheduledGame g, Team team) => new(
        g.Id, team.Id, team.Name, team.MessageGroupId, team.MessageGroup?.Name,
        g.Kind, g.StartsAt, g.EndsAt, g.Summary, g.Location, g.Description,
        g.OpponentName, g.IsHome, g.SeriesId, g.IsCancelled, g.CancelledAt,
        g.TournamentId, g.Tournament?.Name);

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
            .Where(g => g.StartsAt >= from && g.StartsAt <= to && !g.IsCancelled)
            .OrderBy(g => g.StartsAt)
            .Select(g => new ScheduledGameDto(
                g.Id, g.TeamId, g.Team!.Name, g.Team.MessageGroupId, g.Team.MessageGroup != null ? g.Team.MessageGroup.Name : null,
                g.Kind, g.StartsAt, g.EndsAt, g.Summary, g.Location, g.Description,
                g.OpponentName, g.IsHome, g.SeriesId, g.IsCancelled, g.CancelledAt,
                g.TournamentId, g.Tournament != null ? g.Tournament.Name : null))
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
