namespace SoccerSchool.Api.Domain;

/// <summary>
/// Tracks the lifecycle of a single fan-out send. Twilio reports back via the status callback
/// webhook; values mirror Twilio's MessageStatus strings.
/// </summary>
public enum MessageDeliveryStatus
{
    Pending = 0,
    Queued = 1,
    Sent = 2,
    Delivered = 3,
    Failed = 4,
    Undelivered = 5
}
