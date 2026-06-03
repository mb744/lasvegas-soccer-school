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
    /// <summary>Parses a block of text copied directly out of the TeamSnap UI's
    /// "Date / Time / Venue / Game / Team / Score / Score / Team" schedule table
    /// and upserts ScheduledGame rows for the games this team plays in.</summary>
    Task<ScheduleSyncResult> ImportPastedScheduleAsync(int tournamentTeamId, string pastedText, string? teamNameOverride, CancellationToken ct);
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

    public async Task<ScheduleSyncResult> ImportPastedScheduleAsync(int tournamentTeamId, string pastedText, string? teamNameOverride, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(pastedText))
            return new ScheduleSyncResult(false, 0, 0, "Pasted text is empty.");

        var tt = await _db.TournamentTeams
            .Include(x => x.Tournament)
            .Include(x => x.Team)
            .FirstOrDefaultAsync(x => x.Id == tournamentTeamId, ct);
        if (tt is null) return new ScheduleSyncResult(false, 0, 0, "Tournament team not found.");
        if (tt.Team is null) return new ScheduleSyncResult(false, 0, 0, "Team row missing.");

        // Year defaults to the tournament's StartDate year; if missing, the current year.
        var yearHint = tt.Tournament?.StartDate?.Year ?? DateTime.UtcNow.Year;
        // The admin can override the label we search for — TeamSnap rosters often spell the
        // team differently than our internal name ("Las Vegas Soccer School" vs "LVSS").
        var ourName = string.IsNullOrWhiteSpace(teamNameOverride)
            ? tt.Team.Name.Trim()
            : teamNameOverride.Trim();

        var existing = await _db.ScheduledGames
            .Where(g => g.TeamId == tt.TeamId && g.TournamentId == tt.TournamentId)
            .ToListAsync(ct);
        var byUid = existing.ToDictionary(g => g.ExternalUid, g => g, StringComparer.Ordinal);

        var games = ParsePastedSchedule(pastedText, yearHint);
        int added = 0, updated = 0, ours = 0;
        var now = DateTime.UtcNow;
        foreach (var g in games)
        {
            var isHomeUs = NamesMatch(g.HomeName, ourName);
            var isAwayUs = NamesMatch(g.AwayName, ourName);
            if (!isHomeUs && !isAwayUs) continue;
            ours++;
            var opponent = isHomeUs ? g.AwayName : g.HomeName;
            var startsAt = PastedRowToUtc(g.LocalDate, g.LocalTime, g.YearHint);
            if (startsAt is null) continue;
            // Stable per-team uid that survives re-paste: date + time + opponent.
            var uid = $"ts-paste:{tt.Id}:{startsAt.Value:yyyyMMddHHmm}:{Slug(opponent)}";
            var summary = $"{g.HomeName} vs {g.AwayName}";
            if (byUid.TryGetValue(uid, out var row))
            {
                row.StartsAt = startsAt.Value;
                row.Summary = Trim(summary, 512);
                row.Location = Trim(g.Venue, 512);
                row.OpponentName = Trim(opponent, 256);
                row.IsHome = isHomeUs;
                row.LastSeenAt = now;
                updated++;
            }
            else
            {
                _db.ScheduledGames.Add(new ScheduledGame
                {
                    TeamId = tt.TeamId,
                    TournamentId = tt.TournamentId,
                    ExternalUid = uid,
                    StartsAt = startsAt.Value,
                    Summary = Trim(summary, 512),
                    Location = Trim(g.Venue, 512),
                    OpponentName = Trim(opponent, 256),
                    IsHome = isHomeUs,
                    LastSeenAt = now,
                    CreatedAt = now,
                });
                added++;
            }
        }
        tt.LastSyncedAt = now;
        tt.LastSyncMessage = ours == 0
            ? $"Parsed {games.Count} game(s) but none matched team name \"{ourName}\"."
            : $"{added} added, {updated} updated from {ours} matching game(s) in the paste.";
        await _db.SaveChangesAsync(ct);
        return new ScheduleSyncResult(true, added, updated, tt.LastSyncMessage);
    }

    private static bool NamesMatch(string? a, string? b)
    {
        if (string.IsNullOrWhiteSpace(a) || string.IsNullOrWhiteSpace(b)) return false;
        return string.Equals(a.Trim(), b.Trim(), StringComparison.OrdinalIgnoreCase);
    }

    private static string Slug(string? s)
    {
        if (string.IsNullOrWhiteSpace(s)) return string.Empty;
        var arr = s.ToLowerInvariant().Where(c => char.IsLetterOrDigit(c)).ToArray();
        return new string(arr);
    }

    /// <summary>Parses the TeamSnap-table format the admin pastes — typically three lines per
    /// game (header row with MM/DD, time, venue; then two team-name rows). Tolerates extra
    /// blank/score rows between entries.</summary>
    internal static List<PastedScheduleGame> ParsePastedSchedule(string text, int yearHint)
    {
        var lines = text.Split('\n').Select(l => l.TrimEnd('\r').TrimEnd()).ToList();
        var games = new List<PastedScheduleGame>();
        // Header row pattern: "MM/DD\tHH:MM (AM|PM)\tVenue..." (the score columns can be empty).
        var headerRe = new System.Text.RegularExpressions.Regex(
            @"^(\d{1,2}/\d{1,2})\s+(\d{1,2}:\d{2}\s*[AP]M)\s+([^\t]+?)(?:\t|\s{2,}|$)",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        // Also accept tab-delimited variant.
        var headerReTab = new System.Text.RegularExpressions.Regex(
            @"^(\d{1,2}/\d{1,2})\t([^\t]+?)\t([^\t]+?)(?:\t|$)",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);

        for (int i = 0; i < lines.Count; i++)
        {
            var line = lines[i];
            if (string.IsNullOrWhiteSpace(line)) continue;
            string? dateStr = null, timeStr = null, venue = null;
            var m = headerRe.Match(line);
            if (m.Success)
            {
                dateStr = m.Groups[1].Value;
                timeStr = m.Groups[2].Value.Replace(" ", "").ToUpperInvariant();
                venue = m.Groups[3].Value.Trim();
            }
            else
            {
                var mt = headerReTab.Match(line);
                if (!mt.Success) continue;
                dateStr = mt.Groups[1].Value;
                timeStr = mt.Groups[2].Value.Trim();
                venue = mt.Groups[3].Value.Trim();
            }
            // The next two non-blank, non-header lines are home and away.
            string? home = null, away = null;
            int j = i + 1;
            while (j < lines.Count && string.IsNullOrWhiteSpace(lines[j])) j++;
            if (j < lines.Count && !headerRe.IsMatch(lines[j]) && !headerReTab.IsMatch(lines[j]))
            {
                home = lines[j].Trim(); j++;
            }
            while (j < lines.Count && string.IsNullOrWhiteSpace(lines[j])) j++;
            if (j < lines.Count && !headerRe.IsMatch(lines[j]) && !headerReTab.IsMatch(lines[j]))
            {
                away = lines[j].Trim(); j++;
            }
            if (home is null || away is null) continue;
            games.Add(new PastedScheduleGame(dateStr!, timeStr!, venue, home, away, yearHint));
            i = j - 1; // skip past the team rows on the next iteration
        }
        return games;
    }

    /// <summary>Converts a "MM/DD" + "h:mmAM/PM" pasted pair into UTC using the tournament's
    /// year hint and the Pacific timezone (Las Vegas).</summary>
    private static DateTime? PastedRowToUtc(string monthDay, string time, int yearHint)
    {
        if (!System.Text.RegularExpressions.Regex.IsMatch(monthDay, @"^\d{1,2}/\d{1,2}$")) return null;
        var withYear = $"{monthDay}/{yearHint} {time}";
        if (!DateTime.TryParseExact(withYear, "M/d/yyyy h:mmtt",
                CultureInfo.InvariantCulture, DateTimeStyles.None, out var local))
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

    internal record PastedScheduleGame(
        string LocalDate, string LocalTime, string? Venue,
        string HomeName, string AwayName, int YearHint);

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
