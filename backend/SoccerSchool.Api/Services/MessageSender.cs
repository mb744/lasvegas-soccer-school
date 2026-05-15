using System.Text.Json;
using Microsoft.Extensions.Options;
using SoccerSchool.Api.Domain;
using SoccerSchool.Api.Options;
using Twilio;
using Twilio.Rest.Api.V2010.Account;
using Twilio.Types;

namespace SoccerSchool.Api.Services;

/// <summary>
/// Sends a single SMS or WhatsApp message via Twilio MessageResource. Used by the broadcast
/// pipeline to fan out the same body to multiple recipients individually. Replies come back
/// as private 1:1 threads to the Twilio number, not as a group chat — see IConversationService
/// when you need a true reply-all group thread.
/// </summary>
public interface IMessageSender
{
    Task<MessageSendResult> SendAsync(MessageChannel channel, string toPhone, string body, CancellationToken ct);

    /// <summary>
    /// Sends a WhatsApp message using a pre-approved Content template. Required for business-initiated
    /// WhatsApp sends outside the 24-hour customer-service window. The <paramref name="variables"/>
    /// dictionary keys must match the numeric placeholders in the approved template (e.g. "1", "2").
    /// </summary>
    Task<MessageSendResult> SendTemplateAsync(string toPhone, string contentSid, IReadOnlyDictionary<string, string> variables, CancellationToken ct);

    bool IsAvailable(MessageChannel channel);
}

public record MessageSendResult(bool Success, string? TwilioSid, MessageDeliveryStatus Status, string? Message);

public class MessageSender : IMessageSender
{
    private readonly TwilioOptions _twilio;
    private readonly AppOptions _app;
    private readonly ILogger<MessageSender> _logger;

    public MessageSender(IOptions<TwilioOptions> twilio, IOptions<AppOptions> app, ILogger<MessageSender> logger)
    {
        _twilio = twilio.Value;
        _app = app.Value;
        _logger = logger;
    }

    public bool IsAvailable(MessageChannel channel) => channel switch
    {
        MessageChannel.Sms => _twilio.IsSmsConfigured,
        MessageChannel.WhatsApp => _twilio.IsWhatsAppConfigured,
        _ => false
    };

    public async Task<MessageSendResult> SendAsync(MessageChannel channel, string toPhone, string body, CancellationToken ct)
    {
        if (!IsAvailable(channel))
        {
            var key = channel == MessageChannel.WhatsApp ? "Twilio:WhatsAppFromNumber" : "Twilio:SmsFromNumber";
            return new MessageSendResult(false, null, MessageDeliveryStatus.Failed, $"{channel} not configured (set {key}).");
        }

        if (string.IsNullOrWhiteSpace(toPhone))
            return new MessageSendResult(false, null, MessageDeliveryStatus.Failed, "Missing recipient phone.");

        try
        {
            TwilioClient.Init(_twilio.AccountSid, _twilio.AuthToken);

            var (from, to) = channel == MessageChannel.WhatsApp
                ? (new PhoneNumber($"whatsapp:{_twilio.WhatsAppFromNumber}"), new PhoneNumber($"whatsapp:{toPhone}"))
                : (new PhoneNumber(_twilio.SmsFromNumber), new PhoneNumber(toPhone));

            var options = new CreateMessageOptions(to)
            {
                From = from,
                Body = body
            };
            // Without StatusCallback, Twilio only reports the initial accept; we'd never see
            // delivered/failed. Skip the callback for non-https PublicBaseUrl (local dev): Twilio
            // refuses plain-http callbacks, and localhost isn't reachable from their cloud anyway.
            var callback = BuildCallbackUrl();
            if (callback is not null) options.StatusCallback = callback;

            var msg = await MessageResource.CreateAsync(options);

            return msg.ErrorCode is null
                ? new MessageSendResult(true, msg.Sid, MapTwilioStatus(msg.Status?.ToString()), $"queued (status {msg.Status}).")
                : new MessageSendResult(false, msg.Sid, MessageDeliveryStatus.Failed, $"Twilio error {msg.ErrorCode}: {msg.ErrorMessage}");
        }
        catch (Twilio.Exceptions.ApiException ex)
        {
            _logger.LogError(ex, "Twilio {Channel} send failed to {Phone}", channel, toPhone);
            return new MessageSendResult(false, null, MessageDeliveryStatus.Failed, $"Twilio API error: {ex.Code} {ex.Message}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Twilio {Channel} send failed to {Phone}", channel, toPhone);
            return new MessageSendResult(false, null, MessageDeliveryStatus.Failed, $"Twilio error: {ex.Message}");
        }
    }

    public async Task<MessageSendResult> SendTemplateAsync(
        string toPhone,
        string contentSid,
        IReadOnlyDictionary<string, string> variables,
        CancellationToken ct)
    {
        // Content templates are only meaningful on WhatsApp for this app — SMS doesn't have an
        // equivalent business-initiated template concept. So gate on the WhatsApp sender.
        if (!_twilio.IsWhatsAppConfigured)
            return new MessageSendResult(false, null, MessageDeliveryStatus.Failed, "WhatsApp not configured (set Twilio:WhatsAppFromNumber).");
        if (string.IsNullOrWhiteSpace(toPhone))
            return new MessageSendResult(false, null, MessageDeliveryStatus.Failed, "Missing recipient phone.");
        if (string.IsNullOrWhiteSpace(contentSid))
            return new MessageSendResult(false, null, MessageDeliveryStatus.Failed, "Missing ContentSid.");

        try
        {
            TwilioClient.Init(_twilio.AccountSid, _twilio.AuthToken);
            var from = new PhoneNumber($"whatsapp:{_twilio.WhatsAppFromNumber}");
            var to = new PhoneNumber($"whatsapp:{toPhone}");

            var options = new CreateMessageOptions(to)
            {
                From = from,
                ContentSid = contentSid,
                // Twilio expects ContentVariables as a JSON-encoded string of {"1":"foo","2":"bar"}.
                ContentVariables = JsonSerializer.Serialize(variables)
            };
            var callback = BuildCallbackUrl();
            if (callback is not null) options.StatusCallback = callback;

            var msg = await MessageResource.CreateAsync(options);
            return msg.ErrorCode is null
                ? new MessageSendResult(true, msg.Sid, MapTwilioStatus(msg.Status?.ToString()), $"template queued (status {msg.Status}).")
                : new MessageSendResult(false, msg.Sid, MessageDeliveryStatus.Failed, $"Twilio error {msg.ErrorCode}: {msg.ErrorMessage}");
        }
        catch (Twilio.Exceptions.ApiException ex)
        {
            _logger.LogError(ex, "Twilio WhatsApp template send failed to {Phone}", toPhone);
            return new MessageSendResult(false, null, MessageDeliveryStatus.Failed, $"Twilio API error: {ex.Code} {ex.Message}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Twilio WhatsApp template send failed to {Phone}", toPhone);
            return new MessageSendResult(false, null, MessageDeliveryStatus.Failed, $"Twilio error: {ex.Message}");
        }
    }

    private Uri? BuildCallbackUrl()
    {
        if (string.IsNullOrWhiteSpace(_app.PublicBaseUrl)) return null;
        var baseUrl = _app.PublicBaseUrl.TrimEnd('/');
        if (!Uri.TryCreate($"{baseUrl}/api/twilio/status", UriKind.Absolute, out var uri)) return null;
        return uri.Scheme == Uri.UriSchemeHttps ? uri : null;
    }

    public static MessageDeliveryStatus MapTwilioStatus(string? status) => status?.ToLowerInvariant() switch
    {
        "accepted" or "queued" or "scheduled" => MessageDeliveryStatus.Queued,
        "sending" or "sent" => MessageDeliveryStatus.Sent,
        "delivered" or "read" => MessageDeliveryStatus.Delivered,
        "failed" => MessageDeliveryStatus.Failed,
        "undelivered" => MessageDeliveryStatus.Undelivered,
        _ => MessageDeliveryStatus.Pending
    };
}
