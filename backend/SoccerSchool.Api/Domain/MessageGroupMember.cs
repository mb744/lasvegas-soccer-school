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

    /// <summary>Optional link back to the parent account when the member was pulled from registration data.
    /// Null for ad-hoc numbers added by the admin.</summary>
    public int? ParentAccountId { get; set; }
    public ParentAccount? ParentAccount { get; set; }
}
