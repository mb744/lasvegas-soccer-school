using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using SoccerSchool.Api.Data;

namespace SoccerSchool.Api.Services;

/// <summary>A single push to deliver: title/body + an optional data payload the app uses to deep-link
/// (e.g. <c>{ type: "chat", groupId: 12 }</c> or <c>{ type: "event", eventId: 88 }</c>).</summary>
public record PushNotification(string Title, string Body, IReadOnlyDictionary<string, object>? Data = null);

/// <summary>
/// Sends push notifications to parents' devices via the Expo Push API. Expo brokers APNs (iOS) and
/// FCM (Android) so we don't manage either directly. Tokens are looked up from
/// <see cref="Domain.DeviceToken"/>; tokens Expo reports as <c>DeviceNotRegistered</c> are pruned so
/// we stop sending to uninstalled apps. No-op when the user has no registered device.
/// </summary>
public interface IPushSender
{
    /// <summary>Pushes to every active device of each user id. Best-effort: failures are logged, not thrown.</summary>
    Task SendToUsersAsync(IEnumerable<string> userIds, PushNotification notification, CancellationToken ct);
}

public class ExpoPushSender : IPushSender
{
    private const string ExpoEndpoint = "https://exp.host/--/api/v2/push/send";

    private readonly IHttpClientFactory _httpFactory;
    private readonly IServiceProvider _services;
    private readonly ILogger<ExpoPushSender> _logger;

    public ExpoPushSender(IHttpClientFactory httpFactory, IServiceProvider services, ILogger<ExpoPushSender> logger)
    {
        _httpFactory = httpFactory;
        _services = services;
        _logger = logger;
    }

    public async Task SendToUsersAsync(IEnumerable<string> userIds, PushNotification notification, CancellationToken ct)
    {
        var ids = userIds.Distinct().ToList();
        if (ids.Count == 0) return;

        using var scope = _services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var tokens = await db.DeviceTokens
            .Where(d => ids.Contains(d.UserId))
            .Select(d => d.ExpoPushToken)
            .Distinct()
            .ToListAsync(ct);
        if (tokens.Count == 0) return;

        var messages = tokens.Select(t => new ExpoPushMessage
        {
            To = t,
            Title = notification.Title,
            Body = notification.Body,
            Data = notification.Data,
            Sound = "default",
        }).ToList();

        try
        {
            var client = _httpFactory.CreateClient();
            // Expo accepts up to 100 messages per request.
            foreach (var batch in messages.Chunk(100))
            {
                var resp = await client.PostAsJsonAsync(ExpoEndpoint, batch, ct);
                if (!resp.IsSuccessStatusCode)
                {
                    _logger.LogWarning("Expo push returned {Status} for {Count} message(s).", resp.StatusCode, batch.Length);
                    continue;
                }
                await PruneUnregisteredAsync(db, resp, batch, ct);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Expo push send failed for {Count} token(s).", tokens.Count);
        }
    }

    /// <summary>Reads the Expo ticket response and deletes any token it flagged
    /// <c>DeviceNotRegistered</c> (app uninstalled / token rotated).</summary>
    private async Task PruneUnregisteredAsync(AppDbContext db, HttpResponseMessage resp, ExpoPushMessage[] batch, CancellationToken ct)
    {
        try
        {
            var payload = await resp.Content.ReadFromJsonAsync<ExpoTicketResponse>(ct);
            if (payload?.Data is null) return;

            var dead = new List<string>();
            for (var i = 0; i < payload.Data.Count && i < batch.Length; i++)
            {
                var ticket = payload.Data[i];
                if (ticket.Status == "error" && ticket.Details?.Error == "DeviceNotRegistered")
                    dead.Add(batch[i].To);
            }
            if (dead.Count == 0) return;

            var rows = await db.DeviceTokens.Where(d => dead.Contains(d.ExpoPushToken)).ToListAsync(ct);
            if (rows.Count > 0)
            {
                db.DeviceTokens.RemoveRange(rows);
                await db.SaveChangesAsync(ct);
                _logger.LogInformation("Pruned {Count} unregistered push token(s).", rows.Count);
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Could not parse Expo ticket response for pruning.");
        }
    }

    private sealed class ExpoPushMessage
    {
        [JsonPropertyName("to")] public string To { get; set; } = string.Empty;
        [JsonPropertyName("title")] public string Title { get; set; } = string.Empty;
        [JsonPropertyName("body")] public string Body { get; set; } = string.Empty;
        [JsonPropertyName("sound")] public string? Sound { get; set; }
        [JsonPropertyName("data")] public IReadOnlyDictionary<string, object>? Data { get; set; }
    }

    private sealed class ExpoTicketResponse
    {
        [JsonPropertyName("data")] public List<ExpoTicket>? Data { get; set; }
    }

    private sealed class ExpoTicket
    {
        [JsonPropertyName("status")] public string? Status { get; set; }
        [JsonPropertyName("details")] public ExpoTicketDetails? Details { get; set; }
    }

    private sealed class ExpoTicketDetails
    {
        [JsonPropertyName("error")] public string? Error { get; set; }
    }
}
