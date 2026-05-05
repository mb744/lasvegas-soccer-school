using System.ComponentModel.DataAnnotations;

namespace SoccerSchool.Api.Domain;

public class Registration
{
    public int Id { get; set; }

    [Required, MaxLength(80)]
    public string ParentFirstName { get; set; } = string.Empty;

    [Required, MaxLength(80)]
    public string ParentLastName { get; set; } = string.Empty;

    [Required, MaxLength(200)]
    public string AddressLine1 { get; set; } = string.Empty;

    [MaxLength(200)]
    public string? AddressLine2 { get; set; }

    [Required, MaxLength(80)]
    public string City { get; set; } = string.Empty;

    [Required, MaxLength(40)]
    public string State { get; set; } = "NV";

    [Required, MaxLength(20)]
    public string PostalCode { get; set; } = string.Empty;

    [Required, MaxLength(32)]
    public string CellPhone { get; set; } = string.Empty;

    [Required, MaxLength(256)]
    public string Email { get; set; } = string.Empty;

    public Language Language { get; set; } = Language.English;

    public bool WaiverConsent { get; set; }

    public DateTime? WaiverSignedAt { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public List<Player> Players { get; set; } = new();
}
