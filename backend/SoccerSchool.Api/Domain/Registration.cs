using System.ComponentModel.DataAnnotations;

namespace SoccerSchool.Api.Domain;

/// <summary>
/// A season-specific enrollment submitted by a ParentAccount.
/// Parent contact fields are snapshotted at submit time so historical
/// records and waiver PDFs reflect what was agreed to.
/// </summary>
public class Registration
{
    public int Id { get; set; }

    public int ParentAccountId { get; set; }
    public ParentAccount? ParentAccount { get; set; }

    [Required, MaxLength(16)]
    public string Season { get; set; } = string.Empty;

    [Required, MaxLength(80)]
    public string ParentFirstName { get; set; } = string.Empty;

    [Required, MaxLength(80)]
    public string ParentLastName { get; set; } = string.Empty;

    [MaxLength(200)]
    public string AddressLine1 { get; set; } = string.Empty;

    [MaxLength(200)]
    public string? AddressLine2 { get; set; }

    [MaxLength(80)]
    public string City { get; set; } = string.Empty;

    [MaxLength(40)]
    public string State { get; set; } = string.Empty;

    [MaxLength(20)]
    public string PostalCode { get; set; } = string.Empty;

    [Required, MaxLength(32)]
    public string CellPhone { get; set; } = string.Empty;

    [Required, MaxLength(256)]
    public string Email { get; set; } = string.Empty;

    public Language Language { get; set; } = Language.English;

    public bool WaiverConsent { get; set; }

    public DateTime? WaiverSignedAt { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public List<RegistrationPlayer> Players { get; set; } = new();
}
