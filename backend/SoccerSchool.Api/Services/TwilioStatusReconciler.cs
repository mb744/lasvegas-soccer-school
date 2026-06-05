using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SoccerSchool.Api.Data;
using SoccerSchool.Api.Domain;
using SoccerSchool.Api.Options;
using Twilio;
using Twilio.Rest.Api.V2010.Account;

namespace SoccerSchool.Api.Services;

/// <summary>
/// Periodic reconciler that fixes BroadcastRecipient rows whose Twilio status callback we
/// missed (server down, post-200 crash in the webhook, etc.). Scans for recipients stuck in
/// Pending/Queued/Sent past a grace window, fetches the canonical state via Twilio's REST
/// MessageResource by TwilioSid, and updates Status / ErrorCode / StatusMessage in place.
/// Cheap on the Twilio API: capped per cycle and only touches non-terminal rows.
/// </summary>
public class TwilioStatusReconciler : BackgroundService
{
    private readonly IServiceProvider _services;
    private readonly TwilioOptions _twilio;
    private readonly ILogger<TwilioStatusReconciler> _logger;

    /// <summary>How long to wait after a broadcast was created before a recipient is
    /// considered "should have settled by now". Twilio normally delivers terminal status
    /// within seconds; 10 minutes is plenty of headroom for delayed carriers.</summary>
    private static readonly TimeSpan SettleGrace = TimeSpan.FromMinutes(10);

    /// <summary>Loop cadence. 15 min trades freshness for Twilio API budget — at 100 rows
    /// per cycle that's at most ~400 fetches/hour.</summary>
    private static readonly TimeSpan LoopInterval = TimeSpan.FromMinutes(15);

    /// <summary>Cap per cycle so a backlog doesn't slam Twilio if reconciler was offline.</summary>
    private const int MaxPerCycle = 100;

    /// <summary>Don't try to reconcile rows older than this — at that point the message has
    /// almost certainly aged out of Twilio's normal lookup window and the row's never going
    /// to advance. Avoids unbounded re-queries against perma-stuck Pending rows.</summary>
    private static readonly TimeSpan MaxLookback = TimeSpan.FromDays(7);

    public TwilioStatusReconciler(
        IServiceProvider services,
        IOptions<TwilioOptions> twilio,
        ILogger<TwilioStatusReconciler> logger)
    {
        _services = services;
        _twilio = twilio.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (string.IsNullOrWhiteSpace(_twilio.AccountSid) || string.IsNullOrWhiteSpace(_twilio.AuthToken))
        {
            _logger.LogInformation("Twilio not configured; status reconciler is idle.");
            return;
        }

        // Initialize once — TwilioClient is a static singleton inside the SDK.
        TwilioClient.Init(_twilio.AccountSid, _twilio.AuthToken);

        // Stagger the first run a bit so we don't pile on at startup.
        try { await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken); }
        catch (OperationCanceledException) { return; }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RunCycleAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Twilio status reconciler cycle failed; will retry next interval.");
            }
            try { await Task.Delay(LoopInterval, stoppingToken); }
            catch (OperationCanceledException) { break; }
        }
    }

    private async Task RunCycleAsync(CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var settleCutoff = now - SettleGrace;
        var lookbackCutoff = now - MaxLookback;

        using var scope = _services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var candidates = await db.BroadcastRecipients
            .Where(r => r.TwilioSid != null && r.TwilioSid != ""
                && (r.Status == MessageDeliveryStatus.Pending
                    || r.Status == MessageDeliveryStatus.Queued
                    || r.Status == MessageDeliveryStatus.Sent)
                && r.Broadcast!.CreatedAt <= settleCutoff
                && r.Broadcast.CreatedAt >= lookbackCutoff)
            .OrderBy(r => r.Broadcast!.CreatedAt)
            .Take(MaxPerCycle)
            .ToListAsync(ct);

        if (candidates.Count == 0) return;

        int updated = 0;
        foreach (var r in candidates)
        {
            if (ct.IsCancellationRequested) break;
            if (string.IsNullOrWhiteSpace(r.TwilioSid)) continue;
            try
            {
                var msg = await MessageResource.FetchAsync(r.TwilioSid);
                var newStatus = MessageSender.MapTwilioStatus(msg.Status?.ToString());
                var errorCode = msg.ErrorCode?.ToString();
                var statusMessage = string.IsNullOrWhiteSpace(errorCode)
                    ? $"status: {msg.Status} (reconciled)"
                    : $"status: {msg.Status} (error {errorCode}: {msg.ErrorMessage}) (reconciled)";

                if (newStatus != r.Status || (errorCode is not null && r.ErrorCode != errorCode))
                {
                    r.Status = newStatus;
                    r.StatusMessage = statusMessage;
                    r.ErrorCode = string.IsNullOrWhiteSpace(errorCode) ? r.ErrorCode : errorCode;
                    updated++;
                }
            }
            catch (Exception ex)
            {
                // 404 (sid expired/never existed) and rate-limited responses both land here.
                // Log and move on — the row stays for the next cycle if it's still in window.
                _logger.LogDebug(ex, "Reconcile fetch failed for SID {Sid}", r.TwilioSid);
            }
        }

        if (updated > 0)
        {
            await db.SaveChangesAsync(ct);
            _logger.LogInformation("Twilio status reconciler updated {Updated} of {Total} candidate recipient(s).",
                updated, candidates.Count);
        }
    }
}
