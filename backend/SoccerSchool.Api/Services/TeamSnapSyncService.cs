using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using SoccerSchool.Api.Data;
using SoccerSchool.Api.Domain;

namespace SoccerSchool.Api.Services;

/// <summary>
/// Pulls a team's schedule from TeamSnap's public Tournaments API
/// (<c>https://tournaments-api.teamsnap.com/public/events/{eventId}/...</c>).
///
/// Unlike GotSport we don't have to scrape HTML — TeamSnap exposes JSON for matches,
/// match-participants, and participants when called with the events.teamsnap.com Origin.
/// The flow:
///   1. <c>/public/events/{eventId}/match-participants</c> — find every matchId whose
///       <c>participantId</c> matches our stored <see cref="TournamentTeam.TeamSnapParticipantId"/>.
///   2. <c>/public/events/{eventId}/matches</c> — pull match metadata (gameType, completed,
///       results, and — once published — startDate/startTime/venueId).
///   3. <c>/public/events/{eventId}/participants</c> — name lookup for the opposing team.
///
/// Matches without a published <c>startDate+startTime</c> are skipped (ScheduledGame.StartsAt
/// is non-nullable). When TeamSnap finally publishes a date the next re-sync upserts the row.
/// </summary>
public interface ITeamSnapSyncService
{
    Task<ScheduleSyncResult> SyncTournamentTeamAsync(int tournamentTeamId, CancellationToken ct);
}

public class TeamSnapSyncService : ITeamSnapSyncService
{
    private readonly AppDbContext _db;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<TeamSnapSyncService> _logger;

    private const string ApiBase = "https://tournaments-api.teamsnap.com";
    private const string OriginHeader = "https://events.teamsnap.com";

    public TeamSnapSyncService(
        AppDbContext db,
        IHttpClientFactory httpClientFactory,
        ILogger<TeamSnapSyncService> logger)
    {
        _db = db;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public async Task<ScheduleSyncResult> SyncTournamentTeamAsync(int tournamentTeamId, CancellationToken ct)
    {
        var tt = await _db.TournamentTeams
            .Include(x => x.Tournament)
            .FirstOrDefaultAsync(x => x.Id == tournamentTeamId, ct);
        if (tt is null) return new ScheduleSyncResult(false, 0, 0, "Tournament team not found.");
        if (tt.TeamSnapEventId <= 0 || tt.TeamSnapParticipantId <= 0)
            return new ScheduleSyncResult(false, 0, 0, "Missing TeamSnap event/participant IDs.");

        var existing = await _db.ScheduledGames
            .Where(g => g.TeamId == tt.TeamId && g.TournamentId == tt.TournamentId)
            .ToListAsync(ct);

        var r = await SyncAsync(tt.TeamSnapEventId, tt.TeamSnapParticipantId, tt.TeamId, tt.TournamentId, existing, ct);
        tt.LastSyncedAt = DateTime.UtcNow;
        tt.LastSyncMessage = r.Message;
        await _db.SaveChangesAsync(ct);
        return r;
    }

    private async Task<ScheduleSyncResult> SyncAsync(
        int eventId, int participantId, int teamDbId, int? tournamentId,
        IReadOnlyCollection<ScheduledGame> existingGames, CancellationToken ct)
    {
        var http = _httpClientFactory.CreateClient();
        http.Timeout = TimeSpan.FromSeconds(30);
        http.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (compatible; LVSS-ScheduleSync/1.0)");
        http.DefaultRequestHeaders.Add("Origin", OriginHeader);
        http.DefaultRequestHeaders.Add("Referer", OriginHeader + "/");

        TeamSnapEnvelope<List<TsMatchParticipant>>? mpsEnv;
        TeamSnapEnvelope<List<TsMatch>>? matchesEnv;
        TeamSnapEnvelope<List<TsParticipant>>? participantsEnv;
        try
        {
            mpsEnv = await http.GetFromJsonAsync<TeamSnapEnvelope<List<TsMatchParticipant>>>(
                $"{ApiBase}/public/events/{eventId}/match-participants", JsonOpts, ct);
            matchesEnv = await http.GetFromJsonAsync<TeamSnapEnvelope<List<TsMatch>>>(
                $"{ApiBase}/public/events/{eventId}/matches", JsonOpts, ct);
            participantsEnv = await http.GetFromJsonAsync<TeamSnapEnvelope<List<TsParticipant>>>(
                $"{ApiBase}/public/events/{eventId}/participants", JsonOpts, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to fetch TeamSnap data for event {EventId}", eventId);
            return new ScheduleSyncResult(false, 0, 0, $"Fetch failed: {ex.Message}");
        }

        var matchParticipants = mpsEnv?.Data ?? new List<TsMatchParticipant>();
        var matches = (matchesEnv?.Data ?? new List<TsMatch>()).ToDictionary(m => m.Id);
        var participantNames = (participantsEnv?.Data ?? new List<TsParticipant>())
            .Where(p => p.Team is not null)
            .ToDictionary(p => p.Id, p => p.Team!.Name ?? string.Empty);

        // Match-id → ordered list of competitors (number 1 = home convention).
        var byMatch = matchParticipants
            .Where(mp => !mp.Deleted)
            .GroupBy(mp => mp.MatchId)
            .ToDictionary(g => g.Key, g => g.OrderBy(mp => mp.Number ?? 0).ToList());

        // Matches our participant is in.
        var ourMatchIds = byMatch
            .Where(kv => kv.Value.Any(mp => mp.ParticipantId == participantId))
            .Select(kv => kv.Key)
            .ToList();

        var existing = existingGames.ToDictionary(g => g.ExternalUid, g => g, StringComparer.Ordinal);
        int added = 0, updated = 0, scheduled = 0;
        var now = DateTime.UtcNow;

        foreach (var matchId in ourMatchIds)
        {
            if (!matches.TryGetValue(matchId, out var match)) continue;
            var competitors = byMatch[matchId];
            var us = competitors.FirstOrDefault(mp => mp.ParticipantId == participantId);
            var opponent = competitors.FirstOrDefault(mp => mp.ParticipantId != participantId && mp.ParticipantId is not null);
            string? opponentName = opponent?.ParticipantId is int oppId && participantNames.TryGetValue(oppId, out var n) ? n : null;
            // Number 1 = home by TeamSnap convention.
            bool? isHome = us?.Number is int num ? (bool?)(num == 1) : null;
            var homeName = competitors.ElementAtOrDefault(0)?.ParticipantId is int hid && participantNames.TryGetValue(hid, out var hn) ? hn : null;
            var awayName = competitors.ElementAtOrDefault(1)?.ParticipantId is int aid && participantNames.TryGetValue(aid, out var an) ? an : null;
            var summary = $"{homeName ?? "TBD"} vs {awayName ?? "TBD"}";

            var uid = $"ts:{eventId}:{matchId}";
            var startsAt = ParseStartsAtUtc(match.StartDate, match.StartTime);
            if (startsAt is null) continue; // No published date yet — skip; the next sync will pick it up.
            scheduled++;

            var location = match.VenueId?.ToString(CultureInfo.InvariantCulture); // venueId only; venue name needs a separate fetch
            if (existing.TryGetValue(uid, out var game))
            {
                game.StartsAt = startsAt.Value;
                game.Summary = Trim(summary, 512);
                game.Location = Trim(location, 512);
                game.OpponentName = Trim(opponentName, 256);
                game.IsHome = isHome;
                game.LastSeenAt = now;
                game.IsCancelled = match.Canceled;
                updated++;
            }
            else
            {
                _db.ScheduledGames.Add(new ScheduledGame
                {
                    TeamId = teamDbId,
                    TournamentId = tournamentId,
                    ExternalUid = uid,
                    StartsAt = startsAt.Value,
                    Summary = Trim(summary, 512),
                    Location = Trim(location, 512),
                    OpponentName = Trim(opponentName, 256),
                    IsHome = isHome,
                    IsCancelled = match.Canceled,
                    LastSeenAt = now,
                    CreatedAt = now,
                });
                added++;
            }
        }

        string message;
        if (ourMatchIds.Count == 0)
        {
            message = "No matches found on TeamSnap for that participant id. Double-check the IDs.";
        }
        else if (scheduled == 0)
        {
            message = $"Found {ourMatchIds.Count} match(es) but none have a published date/time yet. Re-sync once TeamSnap publishes the schedule.";
        }
        else
        {
            message = $"{added} added, {updated} updated ({ourMatchIds.Count - scheduled} match(es) still without a time).";
        }
        return new ScheduleSyncResult(true, added, updated, message);
    }

    private static DateTime? ParseStartsAtUtc(string? startDate, string? startTime)
    {
        if (string.IsNullOrWhiteSpace(startDate) || string.IsNullOrWhiteSpace(startTime))
            return null;
        // TeamSnap returns "YYYY-MM-DD" + "HH:MM:SS" in the event's local zone. We don't know
        // the zone from the public API, so treat them as Pacific (Las Vegas) to match the
        // existing GotSport behavior. Future improvement: pull the venue timezone if exposed.
        if (!DateTime.TryParseExact(
                $"{startDate.Trim()} {startTime.Trim()[..Math.Min(8, startTime.Trim().Length)]}",
                "yyyy-MM-dd HH:mm:ss",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out var local))
        {
            return null;
        }
        TimeZoneInfo? pacific;
        try { pacific = TimeZoneInfo.FindSystemTimeZoneById("America/Los_Angeles"); }
        catch
        {
            try { pacific = TimeZoneInfo.FindSystemTimeZoneById("Pacific Standard Time"); }
            catch { return null; }
        }
        return TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(local, DateTimeKind.Unspecified), pacific);
    }

    private static string? Trim(string? input, int max) =>
        string.IsNullOrEmpty(input) ? input : (input.Length <= max ? input : input[..max]);

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private sealed class TeamSnapEnvelope<T>
    {
        public T? Data { get; set; }
    }

    private sealed class TsMatch
    {
        public int Id { get; set; }
        public string? GameType { get; set; }
        public bool Completed { get; set; }
        public bool Canceled { get; set; }
        public bool Deleted { get; set; }
        public string? StartDate { get; set; }
        public string? StartTime { get; set; }
        public int? VenueId { get; set; }
    }

    private sealed class TsMatchParticipant
    {
        public int Id { get; set; }
        public int MatchId { get; set; }
        public int? ParticipantId { get; set; }
        public int? Number { get; set; }
        public bool Deleted { get; set; }
    }

    private sealed class TsParticipant
    {
        public int Id { get; set; }
        public TsTeam? Team { get; set; }
    }

    private sealed class TsTeam
    {
        public string? Name { get; set; }
    }
}
