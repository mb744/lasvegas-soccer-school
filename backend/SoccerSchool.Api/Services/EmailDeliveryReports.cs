using System.Text;
using System.Text.Json;
using Azure.Storage.Queues;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SoccerSchool.Api.Data;
using SoccerSchool.Api.Domain;
using SoccerSchool.Api.Options;

namespace SoccerSchool.Api.Services;

/// <summary>
/// Applies one Azure Communication Services email delivery report (an Event Grid event) to the
/// broadcast recipient it belongs to. Correlation is by the ACS operation id the send returned,
/// stored in <see cref="BroadcastRecipient.TwilioSid"/>. Reports for emails that aren't broadcast
/// rows (password resets, admin alerts) simply match nothing.
/// </summary>
public class EmailDeliveryReportProcessor
{
    public const string EventType = "Microsoft.Communication.EmailDeliveryReportReceived";

    private readonly AppDbContext _db;
    private readonly ILogger<EmailDeliveryReportProcessor> _logger;

    public EmailDeliveryReportProcessor(AppDbContext db, ILogger<EmailDeliveryReportProcessor> logger)
    {
        _db = db;
        _logger = logger;
    }

    /// <summary>Returns false only when the message isn't a parseable event; the caller deletes it
    /// either way, since a malformed message will never parse on retry.</summary>
    public async Task<bool> ProcessAsync(string queueMessageText, CancellationToken ct)
    {
        if (!TryParse(queueMessageText, out var root)) return false;

        // Event Grid writes one event per queue message, but accept an array too.
        var events = root.ValueKind == JsonValueKind.Array ? root.EnumerateArray().ToList() : new List<JsonElement> { root };
        foreach (var ev in events)
        {
            if (Str(ev, "eventType") != EventType || !ev.TryGetProperty("data", out var data)) continue;

            var messageId = Str(data, "messageId");
            var status = Str(data, "status");
            if (string.IsNullOrWhiteSpace(messageId) || string.IsNullOrWhiteSpace(status)) continue;

            var mapped = Map(status);
            if (mapped is null) continue; // e.g. "Expanded" (a distribution list fanned out) — not a final outcome.

            var detail = data.TryGetProperty("deliveryStatusDetails", out var d) ? Str(d, "statusMessage") : null;
            var rows = await _db.BroadcastRecipients
                .Where(r => r.TwilioSid == messageId && r.Email != null)
                .ToListAsync(ct);
            foreach (var row in rows)
            {
                row.Status = mapped.Value;
                row.StatusMessage = Truncate(mapped == MessageDeliveryStatus.Delivered
                    ? "Delivered"
                    : string.IsNullOrWhiteSpace(detail) ? status : $"{status}: {detail}", 512);
                row.ErrorCode = mapped == MessageDeliveryStatus.Delivered ? null : Truncate(status, 16);
            }
            if (rows.Count > 0) await _db.SaveChangesAsync(ct);
            else _logger.LogDebug("Email delivery report {Status} for {MessageId} matched no broadcast recipient.", status, messageId);
        }
        return true;
    }

    /// <summary>ACS delivery statuses → our lifecycle. Anything that didn't reach the inbox is
    /// Undelivered, with the ACS status kept in StatusMessage/ErrorCode so admins see why.</summary>
    public static MessageDeliveryStatus? Map(string acsStatus) => acsStatus switch
    {
        "Delivered" => MessageDeliveryStatus.Delivered,
        "Bounced" or "Suppressed" or "FilteredSpam" or "Quarantined" or "Failed" => MessageDeliveryStatus.Undelivered,
        _ => null,
    };

    // Event Grid's storage-queue handler base64-encodes the event; accept raw JSON as well.
    private static bool TryParse(string text, out JsonElement root)
    {
        root = default;
        foreach (var candidate in new[] { text, TryBase64(text) })
        {
            if (string.IsNullOrWhiteSpace(candidate)) continue;
            try
            {
                using var doc = JsonDocument.Parse(candidate);
                root = doc.RootElement.Clone();
                return true;
            }
            catch (JsonException) { }
        }
        return false;
    }

    private static string? TryBase64(string text)
    {
        try { return Encoding.UTF8.GetString(Convert.FromBase64String(text.Trim())); }
        catch (FormatException) { return null; }
    }

    private static string? Str(JsonElement e, string name) =>
        e.ValueKind == JsonValueKind.Object && e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;

    private static string Truncate(string s, int max) => s.Length <= max ? s : s[..max];
}

/// <summary>Drains the Event Grid → Storage queue of email delivery reports. Off unless both the
/// storage connection string and <see cref="StorageOptions.EmailEventsQueueName"/> are set.</summary>
public class EmailDeliveryReportWorker : BackgroundService
{
    private static readonly TimeSpan IdleDelay = TimeSpan.FromSeconds(20);

    private readonly StorageOptions _opts;
    private readonly IServiceScopeFactory _scopes;
    private readonly ILogger<EmailDeliveryReportWorker> _logger;

    public EmailDeliveryReportWorker(IOptions<StorageOptions> opts, IServiceScopeFactory scopes, ILogger<EmailDeliveryReportWorker> logger)
    {
        _opts = opts.Value;
        _scopes = scopes;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_opts.IsConfigured || string.IsNullOrWhiteSpace(_opts.EmailEventsQueueName)) return;

        var queue = new QueueClient(_opts.ConnectionString, _opts.EmailEventsQueueName);
        while (!stoppingToken.IsCancellationRequested)
        {
            var handled = 0;
            try
            {
                var batch = await queue.ReceiveMessagesAsync(maxMessages: 32, visibilityTimeout: TimeSpan.FromMinutes(2), stoppingToken);
                foreach (var msg in batch.Value)
                {
                    using var scope = _scopes.CreateScope();
                    var processor = scope.ServiceProvider.GetRequiredService<EmailDeliveryReportProcessor>();
                    if (!await processor.ProcessAsync(msg.Body.ToString(), stoppingToken))
                        _logger.LogWarning("Discarding unreadable email delivery event {MessageId}.", msg.MessageId);
                    await queue.DeleteMessageAsync(msg.MessageId, msg.PopReceipt, stoppingToken);
                    handled++;
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                // A message whose processing threw stays in the queue and reappears after the
                // visibility timeout, so a DB hiccup retries instead of losing the report.
                _logger.LogError(ex, "Email delivery report worker failed; retrying shortly.");
            }

            if (handled == 0)
            {
                try { await Task.Delay(IdleDelay, stoppingToken); }
                catch (OperationCanceledException) { break; }
            }
        }
    }
}
