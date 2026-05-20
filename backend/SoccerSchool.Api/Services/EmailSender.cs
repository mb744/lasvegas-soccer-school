using System.Net;
using Azure;
using Azure.Communication.Email;
using Microsoft.Extensions.Options;
using SoccerSchool.Api.Options;

namespace SoccerSchool.Api.Services;

/// <summary>
/// Generic outbound email through Azure Communication Services. Mirrors <see cref="IMessageSender"/>
/// for SMS/WhatsApp: a single per-recipient send used by the broadcast fan-out. Distinct from
/// <see cref="IOutreachSender"/> which is specific to signup-link invites.
/// </summary>
public interface IEmailSender
{
    bool IsAvailable { get; }
    Task<EmailSendResult> SendAsync(string toEmail, string subject, string body, CancellationToken ct);
}

public record EmailSendResult(bool Success, string? MessageId, string? Message);

public class EmailSender : IEmailSender
{
    private readonly AcsOptions _acs;
    private readonly ILogger<EmailSender> _logger;

    public EmailSender(IOptions<AcsOptions> acs, ILogger<EmailSender> logger)
    {
        _acs = acs.Value;
        _logger = logger;
    }

    public bool IsAvailable => _acs.IsEmailConfigured;

    public async Task<EmailSendResult> SendAsync(
        string toEmail, string subject, string body, CancellationToken ct)
    {
        if (!IsAvailable)
            return new EmailSendResult(false, null, "Email not configured (set Acs:ConnectionString and Acs:EmailFromAddress).");
        if (string.IsNullOrWhiteSpace(toEmail))
            return new EmailSendResult(false, null, "Missing recipient email.");
        if (string.IsNullOrWhiteSpace(subject))
            return new EmailSendResult(false, null, "Missing subject.");

        try
        {
            var client = new EmailClient(_acs.ConnectionString);
            // Send both plain text (admin-authored) and a minimal HTML wrapper so clients that
            // prefer HTML get something reasonable. The HTML wrap preserves line breaks and
            // escapes user content so injected markup doesn't get rendered.
            var content = new EmailContent(subject)
            {
                PlainText = body,
                Html = WrapAsHtml(body)
            };
            var recipients = new EmailRecipients(new[] { new EmailAddress(toEmail) });
            var message = new EmailMessage(_acs.EmailFromAddress, recipients, content);

            var op = await client.SendAsync(WaitUntil.Completed, message, ct);
            var status = op.Value.Status;
            // op.Id is the ACS operation tracking id (used for status callbacks); the per-message
            // result on op.Value doesn't expose an id in this SDK version.
            return status == EmailSendStatus.Succeeded
                ? new EmailSendResult(true, op.Id, $"Email queued ({status}).")
                : new EmailSendResult(false, op.Id, $"Email status: {status}.");
        }
        catch (RequestFailedException ex)
        {
            _logger.LogError(ex, "ACS email send failed to {Email}", toEmail);
            return new EmailSendResult(false, null, $"ACS email error: {ex.Message}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ACS email send failed to {Email}", toEmail);
            return new EmailSendResult(false, null, $"Email error: {ex.Message}");
        }
    }

    private static string WrapAsHtml(string plain)
    {
        var escaped = WebUtility.HtmlEncode(plain).Replace("\n", "<br/>");
        return $@"<!doctype html><html><body style=""font-family:Arial,sans-serif;font-size:14px;line-height:1.5;color:#222"">{escaped}</body></html>";
    }
}
