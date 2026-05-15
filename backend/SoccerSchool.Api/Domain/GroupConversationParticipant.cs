using System.ComponentModel.DataAnnotations;

namespace SoccerSchool.Api.Domain;

public class GroupConversationParticipant
{
    public int Id { get; set; }

    public int GroupConversationId { get; set; }
    public GroupConversation? Conversation { get; set; }

    [MaxLength(128)]
    public string? Name { get; set; }

    [MaxLength(32)]
    public string Phone { get; set; } = string.Empty;

    /// <summary>Twilio Participant SID (MB...).</summary>
    [MaxLength(64)]
    public string? TwilioParticipantSid { get; set; }
}
