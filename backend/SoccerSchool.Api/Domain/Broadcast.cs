using System.ComponentModel.DataAnnotations;

namespace SoccerSchool.Api.Domain;

/// <summary>
/// A fan-out message: the same body sent individually to one or more recipients via SMS or WhatsApp.
/// Replies return privately to the Twilio number; recipients don't see each other. Individual sends
/// are stored as a Broadcast with a single recipient so everything lives in one log.
/// </summary>
public class Broadcast
{
    public int Id { get; set; }

    public MessageChannel Channel { get; set; } = MessageChannel.Sms;

    /// <summary>English version of the message body. At least one of BodyEn/BodyEs must be set
    /// per broadcast; recipients receive whichever matches their language preference.</summary>
    [MaxLength(2000)]
    public string? BodyEn { get; set; }

    /// <summary>Spanish version of the message body. Optional when the broadcast only targets
    /// English recipients; required (via validation) when any recipient is Spanish.</summary>
    [MaxLength(2000)]
    public string? BodyEs { get; set; }

    /// <summary>Human-readable description of who this was sent to, e.g. "Individual",
    /// "Group: U10 parents", "All active-season parents". Used for the admin log.</summary>
    [MaxLength(256)]
    public string? TargetLabel { get; set; }

    /// <summary>When set, this broadcast was sent via a WhatsApp Content template
    /// (ContentSid + ContentVariables) rather than free-form Body text. Required for
    /// business-initiated WhatsApp sends outside the 24h customer-service window.</summary>
    public int? WhatsAppTemplateId { get; set; }
    public WhatsAppTemplate? WhatsAppTemplate { get; set; }

    /// <summary>JSON object of {"1":"5pm","2":"Sunset Park"} — the values used at send time.
    /// Stored verbatim so the admin log shows what was actually sent.</summary>
    [MaxLength(4000)]
    public string? TemplateVariablesJson { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public List<BroadcastRecipient> Recipients { get; set; } = new();
}
