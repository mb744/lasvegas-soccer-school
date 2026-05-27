using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SoccerSchool.Api.Data;
using SoccerSchool.Api.Domain;
using SoccerSchool.Api.Options;
using SoccerSchool.Api.Services;
using Twilio.Security;

namespace SoccerSchool.Api.Controllers;

/// <summary>
/// Receives Twilio webhooks: per-message delivery status callbacks and inbound replies.
/// Anonymous in the auth sense (no cookie or role) — we validate the X-Twilio-Signature header
/// against our auth token, which proves the request originated from Twilio.
/// </summary>
[ApiController]
[Route("api/twilio")]
[AllowAnonymous]
public class TwilioWebhookController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly TwilioOptions _twilio;
    private readonly IMessageSender _sender;
    private readonly IEmailSender _emailSender;
    private readonly UserManager<ApplicationUser> _users;
    private readonly AppOptions _app;
    private readonly ILogger<TwilioWebhookController> _logger;

    public TwilioWebhookController(
        AppDbContext db,
        IOptions<TwilioOptions> twilio,
        IMessageSender sender,
        IEmailSender emailSender,
        UserManager<ApplicationUser> users,
        IOptions<AppOptions> app,
        ILogger<TwilioWebhookController> logger)
    {
        _db = db;
        _twilio = twilio.Value;
        _sender = sender;
        _emailSender = emailSender;
        _users = users;
        _app = app.Value;
        _logger = logger;
    }

    /// <summary>
    /// Status callback for fan-out sends. Twilio POSTs form-encoded with MessageSid, MessageStatus,
    /// ErrorCode/ErrorMessage as the message moves through queued → sent → delivered/failed.
    /// </summary>
    [HttpPost("status")]
    [Consumes("application/x-www-form-urlencoded")]
    public async Task<IActionResult> Status(CancellationToken ct)
    {
        if (!ValidateSignature()) return Unauthorized();

        var form = Request.Form;
        var sid = form["MessageSid"].ToString();
        var status = form["MessageStatus"].ToString();
        var errorCode = form["ErrorCode"].ToString();
        var errorMessage = form["ErrorMessage"].ToString();

        if (string.IsNullOrWhiteSpace(sid))
            return Ok(); // Acknowledge to stop Twilio retries on a malformed payload.

        var recipient = await _db.BroadcastRecipients.FirstOrDefaultAsync(r => r.TwilioSid == sid, ct);
        if (recipient is null)
        {
            // Could be a conversations-API message or a stray callback. Don't error.
            _logger.LogDebug("Twilio status callback for unknown MessageSid {Sid} (status {Status}).", sid, status);
            return Ok();
        }

        recipient.Status = MessageSender.MapTwilioStatus(status);
        recipient.StatusMessage = string.IsNullOrWhiteSpace(errorCode)
            ? $"status: {status}"
            : $"status: {status} (error {errorCode}: {errorMessage})";
        await _db.SaveChangesAsync(ct);
        return Ok();
    }

    /// <summary>
    /// Inbound SMS or WhatsApp reply. Persists the message into <see cref="InboundMessage"/> so
    /// the admin history can show two-way context, then returns empty TwiML so Twilio doesn't
    /// try to auto-respond. The `whatsapp:` prefix Twilio sets on From/To when the inbound was
    /// over WhatsApp is stripped and used to set Channel.
    /// </summary>
    [HttpPost("inbound")]
    [Consumes("application/x-www-form-urlencoded")]
    public async Task<IActionResult> Inbound(CancellationToken ct)
    {
        if (!ValidateSignature()) return Unauthorized();
        var fromRaw = Request.Form["From"].ToString();
        var toRaw = Request.Form["To"].ToString();
        var body = Request.Form["Body"].ToString();
        var sid = Request.Form["MessageSid"].ToString();

        var channel = fromRaw.StartsWith("whatsapp:", StringComparison.OrdinalIgnoreCase)
            ? MessageChannel.WhatsApp
            : MessageChannel.Sms;
        // Twilio prefixes phone with "whatsapp:" for WhatsApp channel sends. Strip it for storage
        // so the FromPhone column has a clean E.164 value that matches our outbound records.
        var from = StripWhatsAppPrefix(fromRaw);
        var to = StripWhatsAppPrefix(toRaw);

        // Thread the reply onto the most recent broadcast where this phone was a recipient.
        // If they've never been broadcast to, BroadcastId stays null (out-of-the-blue inbound).
        var broadcastId = await _db.BroadcastRecipients
            .Where(r => r.Phone == from)
            .OrderByDescending(r => r.Broadcast!.CreatedAt)
            .Select(r => (int?)r.BroadcastId)
            .FirstOrDefaultAsync(ct);

        var inbound = new InboundMessage
        {
            Channel = channel,
            FromPhone = Truncate(from, 32),
            ToPhone = Truncate(to, 32),
            Body = Truncate(body, 4000),
            TwilioSid = Truncate(sid, 64),
            ReceivedAt = DateTime.UtcNow,
            BroadcastId = broadcastId,
        };
        _db.InboundMessages.Add(inbound);
        await _db.SaveChangesAsync(ct);
        _logger.LogInformation("Inbound {Channel} from {From}: {Body}", channel, from, body);

        // Best-effort side effects. Failures here must NOT bubble up to Twilio — they'd retry the
        // whole webhook and we'd insert duplicate InboundMessages.
        try { await NotifyAdminsAsync(channel, from, body, ct); }
        catch (Exception ex) { _logger.LogWarning(ex, "Admin notification failed for inbound from {From}", from); }

        try { await MaybeAutoReplyAsync(channel, from, ct); }
        catch (Exception ex) { _logger.LogWarning(ex, "Auto-reply to {From} failed", from); }

        return Content("<?xml version=\"1.0\" encoding=\"UTF-8\"?><Response></Response>", "application/xml");
    }

    /// <summary>Sends an admin-configured bilingual "we got your message" reply on the same
    /// channel the parent used. Rate-limited to once per hour per phone so a back-and-forth
    /// conversation doesn't get auto-spammed. Picks the parent's stored language when we can
    /// match them by phone; falls back to English. Reads body and on/off from MessagingSettings.</summary>
    private async Task MaybeAutoReplyAsync(MessageChannel channel, string from, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(from)) return;
        if (!_sender.IsAvailable(channel)) return;

        var settings = await _db.MessagingSettings.AsNoTracking().FirstOrDefaultAsync(ct);
        if (settings is { AutoReplyEnabled: false }) return;

        // Skip if we already auto-replied (or the parent sent another inbound) within the last hour.
        // The check is loose by design: any inbound in the last hour suppresses the auto-reply, so
        // back-to-back parent messages only trigger one canned response.
        var since = DateTime.UtcNow.AddHours(-1);
        var recent = await _db.InboundMessages
            .Where(m => m.FromPhone == from && m.ReceivedAt > since)
            .CountAsync(ct);
        if (recent > 1) return; // 1 = this very inbound we just inserted.

        var parent = await _db.ParentAccounts.FirstOrDefaultAsync(p => p.CellPhone == from, ct);
        var lang = parent?.Language ?? Language.English;
        var body = lang == Language.Spanish
            ? (settings?.AutoReplyTextEs ?? "¡Gracias por escribirnos! Un administrador le responderá pronto.")
            : (settings?.AutoReplyTextEn ?? "Thanks for your message! An admin will reply soon.");

        var result = await _sender.SendAsync(channel, from, body, ct);
        if (!result.Success)
            _logger.LogWarning("Auto-reply to {From} returned {Message}", from, result.Message);
    }

    /// <summary>Emails every Admin-role user a short notice of the inbound. Best-effort; logs and
    /// keeps going on individual delivery failures.</summary>
    private async Task NotifyAdminsAsync(MessageChannel channel, string from, string? body, CancellationToken ct)
    {
        if (!_emailSender.IsAvailable) return;

        var admins = await _users.GetUsersInRoleAsync(Roles.Admin);
        if (admins.Count == 0) return;

        var channelLabel = channel == MessageChannel.WhatsApp ? "WhatsApp" : "SMS";
        var subject = $"New {channelLabel} reply from {from}";
        var emailBody =
            $"A parent just replied on {channelLabel}:\n\n" +
            $"From: {from}\n" +
            $"Time: {DateTime.UtcNow:u}\n\n" +
            $"Message:\n{body ?? "(empty)"}\n\n" +
            $"Open the admin History tab to reply: {_app.PublicBaseUrl?.TrimEnd('/')}/admin/messaging";

        foreach (var admin in admins)
        {
            if (string.IsNullOrWhiteSpace(admin.Email)) continue;
            var result = await _emailSender.SendAsync(admin.Email, subject, emailBody, ct);
            if (!result.Success)
                _logger.LogWarning("Admin notification email to {Email} returned {Message}", admin.Email, result.Message);
        }
    }

    private static string StripWhatsAppPrefix(string s) =>
        s.StartsWith("whatsapp:", StringComparison.OrdinalIgnoreCase) ? s["whatsapp:".Length..] : s;

    private static string Truncate(string? s, int max) =>
        string.IsNullOrEmpty(s) ? string.Empty : (s.Length <= max ? s : s[..max]);

    private bool ValidateSignature()
    {
        if (string.IsNullOrWhiteSpace(_twilio.AuthToken))
        {
            _logger.LogWarning("Twilio webhook hit but AuthToken not configured; rejecting.");
            return false;
        }
        var signature = Request.Headers["X-Twilio-Signature"].ToString();
        if (string.IsNullOrWhiteSpace(signature)) return false;

        // UseForwardedHeaders in Program.cs rewrites Request.Scheme/Host to the original https URL
        // the container app fronted, which is what Twilio signed against. If those were missing,
        // signing would fail because Twilio computes the HMAC over the public URL.
        var url = $"{Request.Scheme}://{Request.Host}{Request.Path}{Request.QueryString}";
        var parameters = Request.Form.ToDictionary(kv => kv.Key, kv => kv.Value.ToString());

        var validator = new RequestValidator(_twilio.AuthToken);
        return validator.Validate(url, parameters, signature);
    }
}
