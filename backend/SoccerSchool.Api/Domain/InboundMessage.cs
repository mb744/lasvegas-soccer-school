using System.ComponentModel.DataAnnotations;

namespace SoccerSchool.Api.Domain;

/// <summary>
/// One inbound SMS or WhatsApp reply received by the Twilio webhook. Persisted so the admin
/// history view can show two-way conversation context (parents asking questions, confirming
/// practice, STOPping, etc.), not just outbound sends.
/// </summary>
public class InboundMessage
{
    public int Id { get; set; }

    public MessageChannel Channel { get; set; } = MessageChannel.Sms;

    [Required, MaxLength(32)]
    public string FromPhone { get; set; } = string.Empty;

    [MaxLength(32)]
    public string? ToPhone { get; set; }

    [MaxLength(4000)]
    public string? Body { get; set; }

    /// <summary>Twilio MessageSid of the inbound message (MM...).</summary>
    [MaxLength(64)]
    public string? TwilioSid { get; set; }

    public DateTime ReceivedAt { get; set; } = DateTime.UtcNow;

    /// <summary>The most recent broadcast where this phone was a recipient, set at receive time.
    /// Threads inbound replies under the outbound that likely prompted them. Null when the sender
    /// has never been on a broadcast (out-of-the-blue inbound — e.g., a new parent texts us first).</summary>
    public int? BroadcastId { get; set; }
    public Broadcast? Broadcast { get; set; }
}
