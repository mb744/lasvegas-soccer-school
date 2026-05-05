using System.ComponentModel.DataAnnotations;

namespace SoccerSchool.Api.Domain;

public class Player
{
    public int Id { get; set; }

    public int RegistrationId { get; set; }
    public Registration? Registration { get; set; }

    [Required, MaxLength(80)]
    public string FirstName { get; set; } = string.Empty;

    [Required, MaxLength(80)]
    public string LastName { get; set; } = string.Empty;

    public DateOnly DateOfBirth { get; set; }

    [Required, MaxLength(40)]
    public string SchoolGrade { get; set; } = string.Empty;

    [Required, MaxLength(10)]
    public string ShirtSize { get; set; } = string.Empty;

    [Required, MaxLength(10)]
    public string ShortSize { get; set; } = string.Empty;

    [Required, MaxLength(10)]
    public string ShoeSize { get; set; } = string.Empty;

    [MaxLength(120)]
    public string? HeardFrom { get; set; }

    // --- Per-player waiver (editable, prepopulated from registration) ---

    [MaxLength(160)]
    public string? WaiverParticipantName { get; set; }

    [MaxLength(120)]
    public string? WaiverTeamName { get; set; }

    [MaxLength(160)]
    public string? WaiverParentGuardianName { get; set; }

    [MaxLength(32)]
    public string? WaiverPhone { get; set; }

    [MaxLength(256)]
    public string? WaiverEmail { get; set; }

    /// <summary>Base64 PNG data URL of the digital signature (e.g. "data:image/png;base64,...").</summary>
    public string? SignatureDataUrl { get; set; }

    public DateTime? SignedAt { get; set; }
}
