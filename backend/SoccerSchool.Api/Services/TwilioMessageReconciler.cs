using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SoccerSchool.Api.Data;
using SoccerSchool.Api.Domain;
using SoccerSchool.Api.Options;
using Twilio;
using Twilio.Rest.Api.V2010.Account;
using Twilio.Types;

namespace SoccerSchool.Api.Services;

public record ReconcileMessagesResult(int OutboundInserted, int InboundInserted, string Message);

/// <summary>
/// Reconciler that pulls Twilio's authoritative message log and inserts any rows our DB is
/// missing. Two failure modes this catches:
///   1. Outbound sends that bypassed the broadcast pipeline (auto-replies pre-`235f586`, future
///      sends from any path that doesn't persist a BroadcastRecipient).
///   2. Inbound webhooks Twilio retried-out (server downtime, post-200 crash) — we never wrote
///      an InboundMessage row, so the parent's reply is invisible to the Inbox.
/// Idempotent via TwilioSid: any message whose SID we already have is skipped.
/// </summary>
public interface ITwilioMessageReconciler
{
    Task<ReconcileMessagesResult> ReconcileAsync(TimeSpan lookback, CancellationToken ct);
}

public class TwilioMessageReconciler : ITwilioMessageReconciler
{
    private readonly IServiceProvider _services;
    private readonly TwilioOptions _twilio;
    private readonly ILogger<TwilioMessageReconciler> _logger;

    /// <summary>Cap per sender per direction so a backlog can't blow up one cycle.
    /// Twilio rate-limits hard on high-volume listing; 1000 covers a normal week of activity.</summary>
    private const int MaxPerSenderPerDirection = 1000;

    public TwilioMessageReconciler(
        IServiceProvider services,
        IOptions<TwilioOptions> twilio,
        ILogger<TwilioMessageReconciler> logger)
    {
        _services = services;
        _twilio = twilio.Value;
        _logger = logger;
    }

    public async Task<ReconcileMessagesResult> ReconcileAsync(TimeSpan lookback, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(_twilio.AccountSid) || string.IsNullOrWhiteSpace(_twilio.AuthToken))
            return new ReconcileMessagesResult(0, 0, "Twilio not configured.");

        TwilioClient.Init(_twilio.AccountSid, _twilio.AuthToken);

        var cutoff = DateTime.UtcNow - lookback;

        // Build the list of (sender-string, channel) pairs to query. WhatsApp from numbers
        // are addressed by Twilio as "whatsapp:+15559579868" — same prefix the sender uses
        // when posting messages out, so the listing call has to match.
        var senders = new List<(string TwilioSender, MessageChannel Channel)>();
        if (!string.IsNullOrWhiteSpace(_twilio.SmsFromNumber))
            senders.Add((_twilio.SmsFromNumber.Trim(), MessageChannel.Sms));
        if (!string.IsNullOrWhiteSpace(_twilio.WhatsAppFromNumber))
            senders.Add(($"whatsapp:{_twilio.WhatsAppFromNumber.Trim()}", MessageChannel.WhatsApp));
        if (senders.Count == 0) return new ReconcileMessagesResult(0, 0, "No sender numbers configured.");

        using var scope = _services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        // Build the set of TwilioSids we already have so we can dedupe without N×1 lookups.
        // Pulls a slightly wider window than `cutoff` to absorb clock skew. For long lookbacks
        // (e.g. the 30-day admin trigger) this pulls more rows but is still bounded.
        var sidWindowCutoff = cutoff.AddMinutes(-30);
        var knownOutboundSids = await db.BroadcastRecipients
            .Where(r => r.TwilioSid != null && r.Broadcast!.CreatedAt >= sidWindowCutoff)
            .Select(r => r.TwilioSid!)
            .ToHashSetAsync(ct);
        var knownInboundSids = await db.InboundMessages
            .Where(m => m.TwilioSid != null && m.ReceivedAt >= sidWindowCutoff)
            .Select(m => m.TwilioSid!)
            .ToHashSetAsync(ct);

        int insertedOut = 0, insertedIn = 0;

        foreach (var (twilioSender, channel) in senders)
        {
            if (ct.IsCancellationRequested) break;

            try
            {
                var outbound = await MessageResource.ReadAsync(
                    from: new PhoneNumber(twilioSender),
                    dateSentAfter: cutoff,
                    limit: MaxPerSenderPerDirection);
                foreach (var msg in outbound)
                {
                    if (string.IsNullOrWhiteSpace(msg.Sid)) continue;
                    if (knownOutboundSids.Contains(msg.Sid)) continue;
                    knownOutboundSids.Add(msg.Sid);
                    InsertOutbound(db, msg, channel);
                    insertedOut++;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Twilio outbound list failed for sender {Sender}", twilioSender);
            }

            try
            {
                var inbound = await MessageResource.ReadAsync(
                    to: new PhoneNumber(twilioSender),
                    dateSentAfter: cutoff,
                    limit: MaxPerSenderPerDirection);
                foreach (var msg in inbound)
                {
                    if (string.IsNullOrWhiteSpace(msg.Sid)) continue;
                    if (knownInboundSids.Contains(msg.Sid)) continue;
                    knownInboundSids.Add(msg.Sid);
                    InsertInbound(db, msg, channel);
                    insertedIn++;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Twilio inbound list failed for sender {Sender}", twilioSender);
            }
        }

        if (insertedOut > 0 || insertedIn > 0)
        {
            await db.SaveChangesAsync(ct);
            _logger.LogInformation("Twilio reconciler inserted {Out} outbound + {In} inbound over {Lookback}.",
                insertedOut, insertedIn, lookback);
        }
        return new ReconcileMessagesResult(insertedOut, insertedIn,
            $"Scanned {senders.Count} sender(s) over {lookback.TotalHours:0.#}h; inserted {insertedOut} outbound + {insertedIn} inbound.");
    }

    private static void InsertOutbound(AppDbContext db, MessageResource msg, MessageChannel channel)
    {
        var toPhone = StripWhatsAppPrefix(msg.To);
        var broadcast = new Broadcast
        {
            Channel = channel,
            BodyEn = msg.Body ?? string.Empty,
            TargetLabel = $"Reconciled outbound to {toPhone}",
            CreatedAt = msg.DateSent.HasValue ? DateTime.SpecifyKind(msg.DateSent.Value, DateTimeKind.Utc) : DateTime.UtcNow,
        };
        var status = MessageSender.MapTwilioStatus(msg.Status?.ToString());
        var errorCode = msg.ErrorCode?.ToString();
        var statusMessage = string.IsNullOrWhiteSpace(errorCode)
            ? $"status: {msg.Status} (reconciled)"
            : $"status: {msg.Status} (error {errorCode}: {msg.ErrorMessage}) (reconciled)";
        broadcast.Recipients.Add(new BroadcastRecipient
        {
            Phone = toPhone ?? string.Empty,
            TwilioSid = msg.Sid,
            Language = Language.English,
            Status = status,
            StatusMessage = statusMessage,
            ErrorCode = string.IsNullOrWhiteSpace(errorCode) ? null : errorCode,
        });
        db.Broadcasts.Add(broadcast);
    }

    private static void InsertInbound(AppDbContext db, MessageResource msg, MessageChannel channel)
    {
        db.InboundMessages.Add(new InboundMessage
        {
            Channel = channel,
            FromPhone = StripWhatsAppPrefix(msg.From) ?? string.Empty,
            ToPhone = StripWhatsAppPrefix(msg.To) ?? string.Empty,
            Body = msg.Body ?? string.Empty,
            TwilioSid = msg.Sid,
            ReceivedAt = msg.DateSent.HasValue ? DateTime.SpecifyKind(msg.DateSent.Value, DateTimeKind.Utc) : DateTime.UtcNow,
        });
    }

    private static string? StripWhatsAppPrefix(object? raw)
    {
        var s = raw?.ToString();
        if (string.IsNullOrEmpty(s)) return s;
        return s.StartsWith("whatsapp:", StringComparison.OrdinalIgnoreCase) ? s["whatsapp:".Length..] : s;
    }
}

/// <summary>Hosted wrapper that runs <see cref="ITwilioMessageReconciler.ReconcileAsync"/>
/// every hour over a 6h lookback. The admin-triggered backfill endpoint uses the same
/// service with a larger window.</summary>
public class TwilioMessageReconcilerBackground : BackgroundService
{
    private readonly ITwilioMessageReconciler _reconciler;
    private readonly ILogger<TwilioMessageReconcilerBackground> _logger;

    private static readonly TimeSpan LoopInterval = TimeSpan.FromHours(1);
    private static readonly TimeSpan LookbackWindow = TimeSpan.FromHours(6);

    public TwilioMessageReconcilerBackground(
        ITwilioMessageReconciler reconciler,
        ILogger<TwilioMessageReconcilerBackground> logger)
    {
        _reconciler = reconciler;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try { await Task.Delay(TimeSpan.FromMinutes(2), stoppingToken); }
        catch (OperationCanceledException) { return; }

        while (!stoppingToken.IsCancellationRequested)
        {
            try { await _reconciler.ReconcileAsync(LookbackWindow, stoppingToken); }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Twilio message reconciler cycle failed; will retry next interval.");
            }
            try { await Task.Delay(LoopInterval, stoppingToken); }
            catch (OperationCanceledException) { break; }
        }
    }
}
