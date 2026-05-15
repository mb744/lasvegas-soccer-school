using Microsoft.AspNetCore.Authorization;
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
    private readonly ILogger<TwilioWebhookController> _logger;

    public TwilioWebhookController(
        AppDbContext db,
        IOptions<TwilioOptions> twilio,
        ILogger<TwilioWebhookController> logger)
    {
        _db = db;
        _twilio = twilio.Value;
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
    /// Inbound SMS or WhatsApp reply. Logged for now; returns empty TwiML so Twilio doesn't
    /// try to auto-respond. Future: surface in the admin UI as replies threaded under the
    /// originating broadcast or conversation.
    /// </summary>
    [HttpPost("inbound")]
    [Consumes("application/x-www-form-urlencoded")]
    public IActionResult Inbound()
    {
        if (!ValidateSignature()) return Unauthorized();
        var from = Request.Form["From"].ToString();
        var body = Request.Form["Body"].ToString();
        _logger.LogInformation("Inbound message from {From}: {Body}", from, body);
        return Content("<?xml version=\"1.0\" encoding=\"UTF-8\"?><Response></Response>", "application/xml");
    }

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
