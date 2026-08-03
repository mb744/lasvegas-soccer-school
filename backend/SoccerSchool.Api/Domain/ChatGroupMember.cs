using System.ComponentModel.DataAnnotations;

namespace SoccerSchool.Api.Domain;

/// <summary>How a member participates — drives moderation and UI styling, not access (every member
/// can read + post).</summary>
public enum ChatMemberRole
{
    Parent = 0,
    Admin = 1,
}

/// <summary>
/// One participant in a <see cref="ChatGroup"/>. Membership is on the <see cref="ParentAccount"/>
/// (the family), so either the owner login or a linked collaborator login sees the group. The
/// denormalized <see cref="DisplayName"/> is what other members see on this member's messages,
/// snapshotted at add-time so a later profile rename doesn't rewrite chat history.
/// </summary>
public class ChatGroupMember
{
    public int Id { get; set; }

    public int ChatGroupId { get; set; }
    public ChatGroup? ChatGroup { get; set; }

    /// <summary>The family this member represents. Null for an admin/staff member who isn't a
    /// parent in the system (they're identified by <see cref="UserId"/> instead).</summary>
    public int? ParentAccountId { get; set; }
    public ParentAccount? ParentAccount { get; set; }

    /// <summary>The specific login for an admin/staff member with no ParentAccount. Null for parent
    /// members, who are resolved through <see cref="ParentAccountId"/> (any of the family's logins).</summary>
    [MaxLength(450)]
    public string? UserId { get; set; }

    [Required, MaxLength(160)]
    public string DisplayName { get; set; } = string.Empty;

    public ChatMemberRole Role { get; set; } = ChatMemberRole.Parent;

    /// <summary>Last message this member has read, for unread-count badges. Null = never opened.</summary>
    public int? LastReadMessageId { get; set; }

    public DateTime AddedAt { get; set; } = DateTime.UtcNow;
}
