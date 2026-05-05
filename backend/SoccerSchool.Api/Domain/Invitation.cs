using System.ComponentModel.DataAnnotations;

namespace SoccerSchool.Api.Domain;

public class Invitation
{
    public int Id { get; set; }

    [MaxLength(64)]
    public string Token { get; set; } = string.Empty;

    [MaxLength(256)]
    public string? Email { get; set; }

    [MaxLength(32)]
    public string? Phone { get; set; }

    public Language Language { get; set; } = Language.English;

    public InvitationStatus Status { get; set; } = InvitationStatus.Pending;

    [MaxLength(1024)]
    public string? StatusMessage { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? SentAt { get; set; }
    public DateTime? OpenedAt { get; set; }
    public DateTime? RegisteredAt { get; set; }

    public int? RegistrationId { get; set; }
    public Registration? Registration { get; set; }
}
