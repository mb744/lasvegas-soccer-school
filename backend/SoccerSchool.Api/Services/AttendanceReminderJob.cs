using Microsoft.EntityFrameworkCore;
using SoccerSchool.Api.Data;
using SoccerSchool.Api.Domain;

namespace SoccerSchool.Api.Services;

/// <summary>
/// Background job that nudges parents to confirm attendance for upcoming games/practices via push.
/// Once per cycle it finds non-cancelled events starting within the reminder window whose rostered
/// players haven't been reminded yet, stamps a Pending <see cref="EventAttendance"/> row
/// (<see cref="EventAttendance.ReminderSentAt"/>) so each family is nudged exactly once, and pushes
/// every player's family. Modeled on <see cref="TwilioStatusReconciler"/>'s scoped-DbContext loop.
/// </summary>
public class AttendanceReminderJob : BackgroundService
{
    private readonly IServiceProvider _services;
    private readonly ILogger<AttendanceReminderJob> _logger;

    /// <summary>How often the job wakes. The window below is wider than this so no event slips
    /// through between cycles.</summary>
    private static readonly TimeSpan LoopInterval = TimeSpan.FromHours(3);

    /// <summary>Only remind for events starting between +6h and +48h. Far enough out that parents can
    /// plan; close enough that the reminder is relevant.</summary>
    private static readonly TimeSpan WindowStart = TimeSpan.FromHours(6);
    private static readonly TimeSpan WindowEnd = TimeSpan.FromHours(48);

    public AttendanceReminderJob(IServiceProvider services, ILogger<AttendanceReminderJob> logger)
    {
        _services = services;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Stagger the first run so startup migrations/seeds finish first.
        try { await Task.Delay(TimeSpan.FromMinutes(2), stoppingToken); }
        catch (OperationCanceledException) { return; }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RunCycleAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Attendance reminder cycle failed; will retry next interval.");
            }
            try { await Task.Delay(LoopInterval, stoppingToken); }
            catch (OperationCanceledException) { break; }
        }
    }

    private async Task RunCycleAsync(CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var from = now + WindowStart;
        var to = now + WindowEnd;

        using var scope = _services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var push = scope.ServiceProvider.GetRequiredService<IPushSender>();

        var events = await db.ScheduledGames
            .Where(g => !g.IsCancelled && g.StartsAt >= from && g.StartsAt <= to)
            .Select(g => new { g.Id, g.TeamId, g.Kind, g.StartsAt, g.Summary, TeamName = g.Team!.Name })
            .ToListAsync(ct);
        if (events.Count == 0) return;

        var reminded = 0;
        foreach (var ev in events)
        {
            if (ct.IsCancellationRequested) break;

            // Rostered players who haven't already been reminded (no row, or a row with no marker).
            var rosterPlayerIds = await db.TeamPlayers
                .Where(tp => tp.TeamId == ev.TeamId)
                .Select(tp => tp.PlayerId)
                .ToListAsync(ct);
            if (rosterPlayerIds.Count == 0) continue;

            var existing = await db.EventAttendances
                .Where(a => a.ScheduledGameId == ev.Id && rosterPlayerIds.Contains(a.PlayerId))
                .ToDictionaryAsync(a => a.PlayerId, a => a, ct);

            // Players to remind this cycle = those with no reminder stamp yet (and not already answered).
            var toRemind = rosterPlayerIds.Where(pid =>
            {
                if (!existing.TryGetValue(pid, out var row)) return true; // no row → remind
                return row.ReminderSentAt is null && row.Status == AttendanceStatus.Pending;
            }).ToList();
            if (toRemind.Count == 0) continue;

            // Map each player → their family logins (owner + collaborators), with the player's name.
            var playerInfo = await db.Players
                .Where(p => toRemind.Contains(p.Id))
                .Select(p => new { p.Id, p.FirstName, p.ParentAccountId, OwnerUserId = p.ParentAccount!.UserId })
                .ToListAsync(ct);

            var parentAccountIds = playerInfo.Select(p => p.ParentAccountId).Distinct().ToList();
            var collaborators = (await db.ParentAccountCollaborators
                .Where(c => parentAccountIds.Contains(c.ParentAccountId))
                .Select(c => new { c.ParentAccountId, c.UserId })
                .ToListAsync(ct))
                .GroupBy(c => c.ParentAccountId)
                .ToDictionary(g => g.Key, g => g.Select(x => x.UserId).ToList());

            var when = TimeForDisplay(ev.StartsAt);
            var kindLabel = ev.Kind == ScheduledEventKind.Practice ? "practice"
                : ev.Kind == ScheduledEventKind.Miscellaneous ? "event" : "game";

            foreach (var p in playerInfo)
            {
                // Stamp / create the attendance row so we never re-remind this player+event.
                if (!existing.TryGetValue(p.Id, out var row))
                {
                    row = new EventAttendance { ScheduledGameId = ev.Id, PlayerId = p.Id, Status = AttendanceStatus.Pending };
                    db.EventAttendances.Add(row);
                }
                row.ReminderSentAt = now;

                var userIds = new List<string>();
                if (!string.IsNullOrEmpty(p.OwnerUserId)) userIds.Add(p.OwnerUserId);
                if (collaborators.TryGetValue(p.ParentAccountId, out var collabIds)) userIds.AddRange(collabIds.Where(u => !string.IsNullOrEmpty(u)));
                if (userIds.Count == 0) continue;

                await push.SendToUsersAsync(userIds, new PushNotification(
                    $"{ev.TeamName}: confirm attendance",
                    $"Is {p.FirstName} coming to the {kindLabel} {when}? Tap to confirm.",
                    new Dictionary<string, object> { ["type"] = "event", ["eventId"] = ev.Id }), ct);
                reminded++;
            }
            await db.SaveChangesAsync(ct);
        }

        if (reminded > 0)
            _logger.LogInformation("Attendance reminder job pushed {Count} player reminder(s).", reminded);
    }

    /// <summary>Pacific wall-clock phrasing for the push body, e.g. "Sat 1:50 PM".</summary>
    private static string TimeForDisplay(DateTime utc)
    {
        try
        {
            var pacific = TimeZoneInfo.FindSystemTimeZoneById("America/Los_Angeles");
            var local = TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), pacific);
            return local.ToString("ddd h:mm tt");
        }
        catch
        {
            return utc.ToString("ddd h:mm tt") + " UTC";
        }
    }
}
