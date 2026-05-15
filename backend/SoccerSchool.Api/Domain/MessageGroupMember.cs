using System.ComponentModel.DataAnnotations;

namespace SoccerSchool.Api.Domain;

public class MessageGroupMember
{
    public int Id { get; set; }

    public int MessageGroupId { get; set; }
    public MessageGroup? Group { get; set; }

    [MaxLength(128)]
    public string? Name { get; set; }

    [MaxLength(32)]
    public string Phone { get; set; } = string.Empty;

    /// <summary>Per-member send language. Each recipient gets the body matching their own setting,
    /// so a single group can mix English- and Spanish-preferring parents without splitting them
    /// into two groups. Defaults to the group's <see cref="MessageGroup.Language"/> at create time
    /// (which itself defaults to English).</summary>
    public Language Language { get; set; } = Language.English;

    /// <summary>Optional link back to the parent account when the member was pulled from registration data.
    /// Null for ad-hoc numbers added by the admin.</summary>
    public int? ParentAccountId { get; set; }
    public ParentAccount? ParentAccount { get; set; }
}
