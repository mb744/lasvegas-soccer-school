using System.ComponentModel.DataAnnotations;

namespace SoccerSchool.Api.Domain;

public class BroadcastRecipient
{
    public int Id { get; set; }

    public int BroadcastId { get; set; }
    public Broadcast? Broadcast { get; set; }

    [MaxLength(128)]
    public string? Name { get; set; }

    [MaxLength(32)]
    public string Phone { get; set; } = string.Empty;

    /// <summary>Which language body was sent to this recipient. Defaults to English; populated
    /// at send time from the recipient's group / parent preference / broadcast default.</summary>
    public Language Language { get; set; } = Language.English;

    public MessageDeliveryStatus Status { get; set; } = MessageDeliveryStatus.Pending;

    /// <summary>Twilio Message SID once accepted, used to correlate the status-callback webhook
    /// back to this row.</summary>
    [MaxLength(64)]
    public string? TwilioSid { get; set; }

    [MaxLength(512)]
    public string? StatusMessage { get; set; }
}
