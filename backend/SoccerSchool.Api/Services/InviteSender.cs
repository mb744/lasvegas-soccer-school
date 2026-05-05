using Azure;
using Azure.Communication.Email;
using Azure.Communication.Sms;
using Microsoft.Extensions.Options;
using SoccerSchool.Api.Domain;
using SoccerSchool.Api.Options;

namespace SoccerSchool.Api.Services;

public interface IInviteSender
{
    Task<SendResult> SendAsync(Invitation invitation, string link, CancellationToken ct);
}

public record SendResult(bool Success, string? Message);

public class InviteSender : IInviteSender
{
    private readonly AcsOptions _acs;
    private readonly ILogger<InviteSender> _logger;

    public InviteSender(IOptions<AcsOptions> acs, ILogger<InviteSender> logger)
    {
        _acs = acs.Value;
        _logger = logger;
    }

    public async Task<SendResult> SendAsync(Invitation invitation, string link, CancellationToken ct)
    {
        if (!string.IsNullOrWhiteSpace(invitation.Email))
        {
            return await SendEmailAsync(invitation, link, ct);
        }
        if (!string.IsNullOrWhiteSpace(invitation.Phone))
        {
            return await SendSmsAsync(invitation, link, ct);
        }
        return new SendResult(false, "No email or phone provided.");
    }

    private async Task<SendResult> SendEmailAsync(Invitation invitation, string link, CancellationToken ct)
    {
        if (!_acs.IsEmailConfigured)
        {
            _logger.LogWarning("ACS email not configured. Skipping send for invite {Token}.", invitation.Token);
            return new SendResult(false, "ACS email not configured (set Acs:ConnectionString and Acs:EmailFromAddress).");
        }

        try
        {
            var client = new EmailClient(_acs.ConnectionString);
            var (subject, plain, html) = BuildEmailContent(invitation.Language, link);

            var content = new EmailContent(subject)
            {
                PlainText = plain,
                Html = html
            };
            var recipients = new EmailRecipients(new[] { new EmailAddress(invitation.Email!) });
            var message = new EmailMessage(_acs.EmailFromAddress, recipients, content);

            var op = await client.SendAsync(WaitUntil.Completed, message, ct);
            var status = op.Value.Status;
            return status == EmailSendStatus.Succeeded
                ? new SendResult(true, $"Email queued ({status}).")
                : new SendResult(false, $"Email status: {status}.");
        }
        catch (RequestFailedException ex)
        {
            _logger.LogError(ex, "ACS email send failed for invite {Token}", invitation.Token);
            return new SendResult(false, $"ACS email error: {ex.Message}");
        }
    }

    private async Task<SendResult> SendSmsAsync(Invitation invitation, string link, CancellationToken ct)
    {
        if (!_acs.IsSmsConfigured)
        {
            _logger.LogWarning("ACS SMS not configured. Skipping send for invite {Token}.", invitation.Token);
            return new SendResult(false, "ACS SMS not configured (set Acs:ConnectionString and Acs:SmsFromNumber).");
        }

        try
        {
            var client = new SmsClient(_acs.ConnectionString);
            var body = BuildSmsBody(invitation.Language, link);
            var response = await client.SendAsync(_acs.SmsFromNumber, invitation.Phone!, body, cancellationToken: ct);
            var result = response.Value;
            return result.Successful
                ? new SendResult(true, $"SMS sent (id {result.MessageId}).")
                : new SendResult(false, $"SMS error: {result.HttpStatusCode} {result.ErrorMessage}");
        }
        catch (RequestFailedException ex)
        {
            _logger.LogError(ex, "ACS SMS send failed for invite {Token}", invitation.Token);
            return new SendResult(false, $"ACS SMS error: {ex.Message}");
        }
    }

    private static (string subject, string plain, string html) BuildEmailContent(Language lang, string link)
    {
        if (lang == Language.Spanish)
        {
            var subject = "Inscripción - Las Vegas Soccer School";
            var plain = $"¡Bienvenido a Las Vegas Soccer School!\n\nComplete su inscripción aquí:\n{link}\n\n¡Nos vemos en el campo!";
            var html = $@"
<div style=""font-family:Arial,sans-serif;max-width:560px;margin:auto"">
  <h2 style=""color:#0a7d3b"">Las Vegas Soccer School</h2>
  <p>¡Bienvenido! Complete la inscripción de su(s) jugador(es) usando el botón a continuación.</p>
  <p style=""text-align:center;margin:32px 0"">
    <a href=""{link}"" style=""background:#0a7d3b;color:#fff;padding:12px 24px;border-radius:6px;text-decoration:none;font-weight:bold"">Comenzar inscripción</a>
  </p>
  <p style=""color:#666;font-size:12px"">Si el botón no funciona, copie este enlace:<br/>{link}</p>
</div>";
            return (subject, plain, html);
        }
        else
        {
            var subject = "Registration - Las Vegas Soccer School";
            var plain = $"Welcome to Las Vegas Soccer School!\n\nComplete your registration here:\n{link}\n\nSee you on the field!";
            var html = $@"
<div style=""font-family:Arial,sans-serif;max-width:560px;margin:auto"">
  <h2 style=""color:#0a7d3b"">Las Vegas Soccer School</h2>
  <p>Welcome! Use the button below to complete your player registration.</p>
  <p style=""text-align:center;margin:32px 0"">
    <a href=""{link}"" style=""background:#0a7d3b;color:#fff;padding:12px 24px;border-radius:6px;text-decoration:none;font-weight:bold"">Start registration</a>
  </p>
  <p style=""color:#666;font-size:12px"">If the button doesn't work, copy this link:<br/>{link}</p>
</div>";
            return (subject, plain, html);
        }
    }

    private static string BuildSmsBody(Language lang, string link) =>
        lang == Language.Spanish
            ? $"Las Vegas Soccer School: complete la inscripción de su jugador: {link}"
            : $"Las Vegas Soccer School: complete your player registration: {link}";
}
