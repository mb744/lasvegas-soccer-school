using System.ComponentModel.DataAnnotations;

namespace SoccerSchool.Api.Domain;

/// <summary>
/// One message posted to a <see cref="ChatGroup"/>. Persisted on every send so the mobile app can
/// load history over REST and replay missed messages after a reconnect; the live fan-out goes over
/// the SignalR <c>ChatHub</c>. Sender identity is denormalized (<see cref="SenderName"/>) so reads
/// don't need a join and history survives profile renames.
/// </summary>
public class ChatMessage
{
    public int Id { get; set; }

    public int ChatGroupId { get; set; }
    public ChatGroup? ChatGroup { get; set; }

    /// <summary>The login that sent it (parent or admin). Lets a client tell "mine" from "theirs".</summary>
    [Required, MaxLength(450)]
    public string SenderUserId { get; set; } = string.Empty;

    [Required, MaxLength(160)]
    public string SenderName { get; set; } = string.Empty;

    /// <summary>True when sent by an admin/coach from the web — clients render these distinctly.</summary>
    public bool IsFromAdmin { get; set; }

    [Required, MaxLength(4000)]
    public string Body { get; set; } = string.Empty;

    public DateTime SentAt { get; set; } = DateTime.UtcNow;
}
