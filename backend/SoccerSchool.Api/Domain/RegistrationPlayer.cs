using System.ComponentModel.DataAnnotations;

namespace SoccerSchool.Api.Domain;

/// <summary>
/// Per-season per-player enrollment row: grade/sizes that change with the kid,
/// "heard from" attribution for this registration, and a fresh waiver signature.
/// </summary>
public class RegistrationPlayer
{
    public int Id { get; set; }

    public int RegistrationId { get; set; }
    public Registration? Registration { get; set; }

    public int PlayerId { get; set; }
    public Player? Player { get; set; }

    [Required, MaxLength(40)]
    public string SchoolGrade { get; set; } = string.Empty;

    [Required, MaxLength(10)]
    public string UniformSize { get; set; } = string.Empty;

    [Required, MaxLength(10)]
    public string ShoeSize { get; set; } = string.Empty;

    [MaxLength(120)]
    public string? HeardFrom { get; set; }

    // --- Per-season waiver snapshot (editable, prepopulated from parent profile) ---

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
