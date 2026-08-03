using System.ComponentModel.DataAnnotations;

namespace SoccerSchool.Api.Domain;

/// <summary>
/// A native in-app group chat. Admins create these on the web (optionally seeded from a team
/// roster); members are parents (<see cref="ChatGroupMember"/>) who read and post from the mobile
/// app over the SignalR <c>ChatHub</c>. Distinct from <see cref="GroupConversation"/>, which is a
/// Twilio Conversations (SMS/WhatsApp) thread — this one is free, real-time, and app-only.
/// </summary>
public class ChatGroup
{
    public int Id { get; set; }

    [Required, MaxLength(128)]
    public string Title { get; set; } = string.Empty;

    /// <summary>Optional link to the team this group was created for. Informational only — membership
    /// is the explicit <see cref="Members"/> list, not the live roster, so adding a player to the team
    /// later doesn't silently add a stranger to an existing chat.</summary>
    public int? TeamId { get; set; }
    public Team? Team { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public List<ChatGroupMember> Members { get; set; } = new();
    public List<ChatMessage> Messages { get; set; } = new();
}
