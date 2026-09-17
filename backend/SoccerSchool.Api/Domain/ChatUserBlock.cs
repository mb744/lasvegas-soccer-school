using System.ComponentModel.DataAnnotations;

namespace SoccerSchool.Api.Domain;

/// <summary>
/// A parent muting another chat participant. The <see cref="BlockerUserId"/> stops seeing any
/// messages authored by <see cref="BlockedUserId"/> in any group they share — enforced server-side
/// when listing messages and mirrored client-side for the live SignalR fan-out. Required by
/// App Store Guideline 1.2 for apps that surface user-generated content.
/// </summary>
public class ChatUserBlock
{
    public int Id { get; set; }

    [Required, MaxLength(450)]
    public string BlockerUserId { get; set; } = string.Empty;

    [Required, MaxLength(450)]
    public string BlockedUserId { get; set; } = string.Empty;

    public DateTime BlockedAt { get; set; } = DateTime.UtcNow;
}
