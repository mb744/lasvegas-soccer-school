using System.ComponentModel.DataAnnotations;

namespace SoccerSchool.Api.Domain;

/// <summary>
/// Admin-curated named recipient list for broadcasts and group chats. Distinct from
/// dynamic groups (e.g. "all active-season parents") which are resolved at send time
/// from registration data and not stored.
/// </summary>
public class MessageGroup
{
    public int Id { get; set; }

    [MaxLength(128)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(512)]
    public string? Description { get; set; }

    /// <summary>Send language for this group. When a broadcast targets the group, every recipient
    /// gets the body in this language regardless of the admin's UI language. Default English.</summary>
    public Language Language { get; set; } = Language.English;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public List<MessageGroupMember> Members { get; set; } = new();
}
