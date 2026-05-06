using Azure;
using Azure.Communication.Email;
using Azure.Communication.Sms;
using Microsoft.Extensions.Options;
using SoccerSchool.Api.Domain;
using SoccerSchool.Api.Options;
using Twilio;
using Twilio.Rest.Api.V2010.Account;
using Twilio.Types;

namespace SoccerSchool.Api.Services;

public interface IOutreachSender
{
    Task<SendResult> SendAsync(Outreach outreach, string link, CancellationToken ct);
}

public record SendResult(bool Success, string? Message);

public class OutreachSender : IOutreachSender
{
    private readonly AcsOptions _acs;
    private readonly TwilioOptions _twilio;
    private readonly ILogger<OutreachSender> _logger;

    public OutreachSender(IOptions<AcsOptions> acs, IOptions<TwilioOptions> twilio, ILogger<OutreachSender> logger)
    {
        _acs = acs.Value;
        _twilio = twilio.Value;
        _logger = logger;
    }

    public async Task<SendResult> SendAsync(Outreach outreach, string link, CancellationToken ct)
    {
        if (!string.IsNullOrWhiteSpace(outreach.Email))
        {
            return await SendEmailAsync(outreach, link, ct);
        }
        if (!string.IsNullOrWhiteSpace(outreach.Phone))
        {
            return await SendSmsAsync(outreach, link, ct);
        }
        return new SendResult(false, "No email or phone provided.");
    }

    private async Task<SendResult> SendEmailAsync(Outreach outreach, string link, CancellationToken ct)
    {
        if (!_acs.IsEmailConfigured)
        {
            _logger.LogWarning("ACS email not configured. Skipping send for outreach {Id}.", outreach.Id);
            return new SendResult(false, "ACS email not configured (set Acs:ConnectionString and Acs:EmailFromAddress).");
        }

        try
        {
            var client = new EmailClient(_acs.ConnectionString);
            var (subject, plain, html) = BuildEmailContent(outreach.Language, link);

            var content = new EmailContent(subject)
            {
                PlainText = plain,
                Html = html
            };
            var recipients = new EmailRecipients(new[] { new EmailAddress(outreach.Email!) });
            var message = new EmailMessage(_acs.EmailFromAddress, recipients, content);

            var op = await client.SendAsync(WaitUntil.Completed, message, ct);
            var status = op.Value.Status;
            return status == EmailSendStatus.Succeeded
                ? new SendResult(true, $"Email queued ({status}).")
                : new SendResult(false, $"Email status: {status}.");
        }
        catch (RequestFailedException ex)
        {
            _logger.LogError(ex, "ACS email send failed for outreach {Id}", outreach.Id);
            return new SendResult(false, $"ACS email error: {ex.Message}");
        }
    }

    private async Task<SendResult> SendSmsAsync(Outreach outreach, string link, CancellationToken ct)
    {
        // Twilio takes precedence when configured. ACS is the legacy fallback,
        // useful only if you ever turn Twilio off again.
        if (_twilio.IsSmsConfigured)
        {
            return await SendSmsViaTwilioAsync(outreach, link, ct);
        }
        if (_acs.IsSmsConfigured)
        {
            return await SendSmsViaAcsAsync(outreach, link, ct);
        }
        _logger.LogWarning("No SMS provider configured. Skipping send for outreach {Id}.", outreach.Id);
        return new SendResult(false, "SMS not configured (set either Twilio:* or Acs:SmsFromNumber).");
    }

    private async Task<SendResult> SendSmsViaTwilioAsync(Outreach outreach, string link, CancellationToken ct)
    {
        try
        {
            // Twilio's static initializer is global; safe to call repeatedly.
            TwilioClient.Init(_twilio.AccountSid, _twilio.AuthToken);
            var body = BuildSmsBody(outreach.Language, link);
            var msg = await MessageResource.CreateAsync(
                to: new PhoneNumber(outreach.Phone!),
                from: new PhoneNumber(_twilio.SmsFromNumber),
                body: body);
            // Twilio's initial Status is usually "queued" or "accepted"; delivery is async via webhooks.
            return msg.ErrorCode is null
                ? new SendResult(true, $"Twilio SMS queued (sid {msg.Sid}, status {msg.Status}).")
                : new SendResult(false, $"Twilio error {msg.ErrorCode}: {msg.ErrorMessage}");
        }
        catch (Twilio.Exceptions.ApiException ex)
        {
            _logger.LogError(ex, "Twilio SMS send failed for outreach {Id}", outreach.Id);
            return new SendResult(false, $"Twilio API error: {ex.Code} {ex.Message}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Twilio SMS send failed for outreach {Id}", outreach.Id);
            return new SendResult(false, $"Twilio error: {ex.Message}");
        }
    }

    private async Task<SendResult> SendSmsViaAcsAsync(Outreach outreach, string link, CancellationToken ct)
    {
        try
        {
            var client = new SmsClient(_acs.ConnectionString);
            var body = BuildSmsBody(outreach.Language, link);
            var response = await client.SendAsync(_acs.SmsFromNumber, outreach.Phone!, body, cancellationToken: ct);
            var result = response.Value;
            return result.Successful
                ? new SendResult(true, $"ACS SMS sent (id {result.MessageId}).")
                : new SendResult(false, $"ACS SMS error: {result.HttpStatusCode} {result.ErrorMessage}");
        }
        catch (RequestFailedException ex)
        {
            _logger.LogError(ex, "ACS SMS send failed for outreach {Id}", outreach.Id);
            return new SendResult(false, $"ACS SMS error: {ex.Message}");
        }
    }

    private static (string subject, string plain, string html) BuildEmailContent(Language lang, string link)
    {
        if (lang == Language.Spanish)
        {
            var subject = "Inscripción - Las Vegas Soccer School";
            var plain = $"¡Bienvenido a Las Vegas Soccer School!\n\nCree su cuenta y complete su inscripción aquí:\n{link}\n\n¡Nos vemos en el campo!";
            var html = $@"
<div style=""font-family:Arial,sans-serif;max-width:560px;margin:auto"">
  <h2 style=""color:#0a7d3b"">Las Vegas Soccer School</h2>
  <p>¡Bienvenido! Cree una cuenta e inscriba a su(s) jugador(es) usando el botón a continuación.</p>
  <p style=""text-align:center;margin:32px 0"">
    <a href=""{link}"" style=""background:#0a7d3b;color:#fff;padding:12px 24px;border-radius:6px;text-decoration:none;font-weight:bold"">Crear cuenta e inscribirse</a>
  </p>
  <p style=""color:#666;font-size:12px"">Si el botón no funciona, copie este enlace:<br/>{link}</p>
</div>";
            return (subject, plain, html);
        }
        else
        {
            var subject = "Registration - Las Vegas Soccer School";
            var plain = $"Welcome to Las Vegas Soccer School!\n\nCreate your account and complete your registration here:\n{link}\n\nSee you on the field!";
            var html = $@"
<div style=""font-family:Arial,sans-serif;max-width:560px;margin:auto"">
  <h2 style=""color:#0a7d3b"">Las Vegas Soccer School</h2>
  <p>Welcome! Create an account and register your player(s) using the button below.</p>
  <p style=""text-align:center;margin:32px 0"">
    <a href=""{link}"" style=""background:#0a7d3b;color:#fff;padding:12px 24px;border-radius:6px;text-decoration:none;font-weight:bold"">Create account and register</a>
  </p>
  <p style=""color:#666;font-size:12px"">If the button doesn't work, copy this link:<br/>{link}</p>
</div>";
            return (subject, plain, html);
        }
    }

    private static string BuildSmsBody(Language lang, string link) =>
        lang == Language.Spanish
            ? $"Las Vegas Soccer School: complete la inscripcion de su jugador: {link} Responda STOP para cancelar, HELP para ayuda."
            : $"Las Vegas Soccer School: complete your player registration: {link} Reply STOP to opt out, HELP for help.";
}
