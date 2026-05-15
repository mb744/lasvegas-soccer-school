using System.ComponentModel.DataAnnotations;

namespace SoccerSchool.Api.Domain;

/// <summary>
/// A true Twilio Conversations group chat where all participants share one thread and can reply-all.
/// Backed by a Twilio Conversation resource (CH... SID). Distinct from a Broadcast (fan-out) where
/// each recipient gets a separate 1:1 thread.
/// </summary>
public class GroupConversation
{
    public int Id { get; set; }

    [MaxLength(128)]
    public string Title { get; set; } = string.Empty;

    public MessageChannel Channel { get; set; } = MessageChannel.Sms;

    /// <summary>Twilio Conversation SID (CH...).</summary>
    [MaxLength(64)]
    public string TwilioConversationSid { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public List<GroupConversationParticipant> Participants { get; set; } = new();
}
