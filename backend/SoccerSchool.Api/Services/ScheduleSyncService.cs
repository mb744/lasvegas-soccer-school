using System.Globalization;
using System.Text.RegularExpressions;
using AngleSharp;
using AngleSharp.Dom;
using AngleSharp.Html.Parser;
using Microsoft.EntityFrameworkCore;
using SoccerSchool.Api.Data;
using SoccerSchool.Api.Domain;

namespace SoccerSchool.Api.Services;

/// <summary>
/// Pulls a team's match schedule by scraping the public GotSport event page
/// <c>https://system.gotsport.com/org_event/events/{EventId}/schedules?team={TeamId}</c>.
///
/// GotSport has no public API (their own ToS disclaims one) and the iCal feed isn't reliably
/// exposed across team configurations. The event schedule page is anonymous-public though, and
/// contains every field we need: date, time (Pacific), home/away with explicit (H)/(A) suffixes,
/// venue, and a stable match #. Detection of "us" is exact: we match on the GotSport team ID in
/// the team link's <c>team=</c> query param, not on fuzzy name comparison.
///
/// This is brittle to DOM changes by definition. If GotSport restructures the page we adapt the
/// selectors. The trade vs no integration is worth it; the trade vs a hypothetical API isn't a
/// trade we get to make.
/// </summary>
public interface IScheduleSyncService
{
    Task<ScheduleSyncResult> SyncTeamAsync(int teamId, CancellationToken ct);
}

public record ScheduleSyncResult(bool Success, int Added, int Updated, string Message);

public class ScheduleSyncService : IScheduleSyncService
{
    private readonly AppDbContext _db;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<ScheduleSyncService> _logger;

    public ScheduleSyncService(
        AppDbContext db,
        IHttpClientFactory httpClientFactory,
        ILogger<ScheduleSyncService> logger)
    {
        _db = db;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public async Task<ScheduleSyncResult> SyncTeamAsync(int teamId, CancellationToken ct)
    {
        var team = await _db.Teams.Include(t => t.Games).FirstOrDefaultAsync(t => t.Id == teamId, ct);
        if (team is null) return new ScheduleSyncResult(false, 0, 0, "Team not found.");
        if (team.GotSportEventId <= 0 || team.GotSportTeamId <= 0)
            return await FailAsync(team, "Team is missing GotSport event/team IDs.", ct);

        var url = $"https://system.gotsport.com/org_event/events/{team.GotSportEventId}/schedules?team={team.GotSportTeamId}";

        string html;
        try
        {
            var http = _httpClientFactory.CreateClient();
            http.Timeout = TimeSpan.FromSeconds(30);
            // GotSport returns 406 for unspecified Accept; a browser-y header avoids that.
            http.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (compatible; LVSS-ScheduleSync/1.0)");
            html = await http.GetStringAsync(url, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to fetch GotSport schedule for team {TeamId}", teamId);
            return await FailAsync(team, $"Fetch failed: {ex.Message}", ct);
        }

        IHtmlParser parser = new HtmlParser();
        var doc = await parser.ParseDocumentAsync(html, ct);

        var existing = team.Games.ToDictionary(g => g.ExternalUid, g => g, StringComparer.Ordinal);
        int added = 0, updated = 0;
        var now = DateTime.UtcNow;
        var ourTeamId = team.GotSportTeamId.ToString(CultureInfo.InvariantCulture);

        // .row.public-match is the per-match anchor. Each one has two team links (.text-primary)
        // with team=XXX in their href and a (H)/(A) suffix in the text. The date/time and venue
        // live in surrounding markup that we walk to find — see PaseMatchContext below.
        var matchRows = doc.QuerySelectorAll(".row.public-match");
        var seenUids = new HashSet<string>();

        foreach (var row in matchRows)
        {
            var teamLinks = row.QuerySelectorAll("a.text-primary[href*='/schedules?team=']").ToList();
            if (teamLinks.Count < 2) continue; // standings/orphan link; skip

            string? homeName = null, awayName = null, homeTeamIdStr = null, awayTeamIdStr = null;
            foreach (var link in teamLinks)
            {
                var raw = link.TextContent.Trim();
                var isHome = raw.EndsWith("(H)", StringComparison.Ordinal);
                var isAway = raw.EndsWith("(A)", StringComparison.Ordinal);
                if (!isHome && !isAway) continue;
                var name = raw[..^3].Trim(); // strip " (H)" or " (A)"
                var tid = ExtractTeamId(link.GetAttribute("href"));
                if (isHome) { homeName = name; homeTeamIdStr = tid; }
                else { awayName = name; awayTeamIdStr = tid; }
            }
            if (homeName is null || awayName is null) continue;

            // Date and time are siblings of the team columns inside the same .row.public-match.
            var timeNode = row.QuerySelector(".label.label-light");
            var dateNode = row.QuerySelector(".fa-calendar")?.ParentElement;
            var (startsAt, parseOk) = ParsePacificDateTime(dateNode?.TextContent, timeNode?.TextContent);
            if (!parseOk) continue;

            // Venue + match # are in the next sibling .row block.
            string? venue = null;
            string? matchNumber = null;
            var contextRow = row.NextElementSibling;
            while (contextRow is not null)
            {
                var v = contextRow.QuerySelector("h5 a[href*='/schedules?pitch=']");
                if (v is not null) venue = v.TextContent.Trim();
                var m = contextRow.QuerySelector(".text-muted.pull-right");
                if (m is not null) matchNumber = m.TextContent.Trim();
                if (venue is not null || matchNumber is not null) break;
                contextRow = contextRow.NextElementSibling;
            }

            // Match number is the most stable per-game key inside one event; combine with the
            // event id (implicit per team here) for global uniqueness.
            var uid = !string.IsNullOrWhiteSpace(matchNumber)
                ? $"gs:{team.GotSportEventId}:{matchNumber.TrimStart('#')}"
                : $"gs:{team.GotSportEventId}:{startsAt:O}:{homeName}:{awayName}".GetHashCode().ToString("x");
            if (uid.Length > 256) uid = uid[..256];
            if (!seenUids.Add(uid)) continue; // .visible-xs/.hidden-xs duplicate dedup

            // Determine home/away for *our* team and pick the opponent.
            bool? isOurTeamHome =
                homeTeamIdStr == ourTeamId ? true :
                awayTeamIdStr == ourTeamId ? false :
                null;
            var opponent = isOurTeamHome == true ? awayName : isOurTeamHome == false ? homeName : null;
            var summary = $"{homeName} vs {awayName}".Trim();

            if (existing.TryGetValue(uid, out var game))
            {
                game.StartsAt = startsAt;
                game.Summary = Trim(summary, 512);
                game.Location = Trim(venue, 512);
                game.OpponentName = Trim(opponent, 256);
                game.IsHome = isOurTeamHome;
                game.LastSeenAt = now;
                updated++;
            }
            else
            {
                team.Games.Add(new ScheduledGame
                {
                    TeamId = team.Id,
                    ExternalUid = uid,
                    StartsAt = startsAt,
                    Summary = Trim(summary, 512),
                    Location = Trim(venue, 512),
                    OpponentName = Trim(opponent, 256),
                    IsHome = isOurTeamHome,
                    LastSeenAt = now,
                    CreatedAt = now
                });
                added++;
            }
        }

        team.LastSyncedAt = now;
        team.LastSyncMessage = added + updated == 0
            ? "No matches found on the GotSport page. Double-check the event/team IDs."
            : $"{added} added, {updated} updated.";
        await _db.SaveChangesAsync(ct);

        return new ScheduleSyncResult(true, added, updated, team.LastSyncMessage);
    }

    private static string? ExtractTeamId(string? href)
    {
        if (string.IsNullOrEmpty(href)) return null;
        // Cheaper than parsing as a Uri — these hrefs are relative.
        var m = Regex.Match(href, @"team=(\d+)");
        return m.Success ? m.Groups[1].Value : null;
    }

    /// <summary>Combines "Saturday, May 16, 2026" + " 1:50PM PDT PDT" into UTC. Treats the page's
    /// time as Pacific because that's what GotSport renders for our local events; if we ever
    /// schedule a tournament in another zone we'll need to pull the offset from the suffix.</summary>
    private static (DateTime startsAt, bool ok) ParsePacificDateTime(string? rawDate, string? rawTime)
    {
        if (string.IsNullOrWhiteSpace(rawDate) || string.IsNullOrWhiteSpace(rawTime))
            return (default, false);

        // Date text often has the calendar icon glyph residue; pull the date phrase out cleanly.
        var dateMatch = Regex.Match(rawDate, @"(January|February|March|April|May|June|July|August|September|October|November|December)\s+\d{1,2},\s+\d{4}");
        if (!dateMatch.Success) return (default, false);

        // Time text looks like " 1:50PM PDT PDT" — the doubled zone is a GotSport template quirk.
        var timeMatch = Regex.Match(rawTime, @"(\d{1,2}:\d{2})\s*([AP]M)", RegexOptions.IgnoreCase);
        if (!timeMatch.Success) return (default, false);

        var dateStr = dateMatch.Value;
        var timeStr = $"{timeMatch.Groups[1].Value}{timeMatch.Groups[2].Value.ToUpperInvariant()}";
        var combined = $"{dateStr} {timeStr}";

        if (!DateTime.TryParseExact(combined, "MMMM d, yyyy h:mmtt",
                CultureInfo.InvariantCulture, DateTimeStyles.None, out var local))
            return (default, false);

        // Convert Pacific (DST-aware) → UTC. "America/Los_Angeles" works on Linux container hosts
        // and on .NET 8+ Windows hosts via the cross-platform TZ database.
        TimeZoneInfo? pacific;
        try
        {
            pacific = TimeZoneInfo.FindSystemTimeZoneById("America/Los_Angeles");
        }
        catch
        {
            try { pacific = TimeZoneInfo.FindSystemTimeZoneById("Pacific Standard Time"); }
            catch { return (default, false); }
        }
        var utc = TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(local, DateTimeKind.Unspecified), pacific);
        return (utc, true);
    }

    private async Task<ScheduleSyncResult> FailAsync(Team team, string message, CancellationToken ct)
    {
        team.LastSyncedAt = DateTime.UtcNow;
        team.LastSyncMessage = message;
        await _db.SaveChangesAsync(ct);
        return new ScheduleSyncResult(false, 0, 0, message);
    }

    private static string? Trim(string? input, int max) =>
        string.IsNullOrEmpty(input) ? input : (input.Length <= max ? input : input[..max]);
}
