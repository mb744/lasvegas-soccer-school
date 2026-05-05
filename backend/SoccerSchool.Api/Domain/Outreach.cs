using System.ComponentModel.DataAnnotations;

namespace SoccerSchool.Api.Domain;

/// <summary>
/// Admin-only tracking record. Admin enters a parent's email or phone, the link is
/// sent, and the row's status follows the funnel:
/// Pending -> Sent -> AccountCreated -> Registered.
/// No token: matches by email/phone when accounts/registrations are created.
/// </summary>
public class Outreach
{
    public int Id { get; set; }

    [MaxLength(256)]
    public string? Email { get; set; }

    [MaxLength(32)]
    public string? Phone { get; set; }

    public Language Language { get; set; } = Language.English;

    public OutreachStatus Status { get; set; } = OutreachStatus.Pending;

    [MaxLength(1024)]
    public string? StatusMessage { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? SentAt { get; set; }
    public DateTime? AccountCreatedAt { get; set; }
    public DateTime? RegisteredAt { get; set; }

    public int? ParentAccountId { get; set; }
    public ParentAccount? ParentAccount { get; set; }
}
