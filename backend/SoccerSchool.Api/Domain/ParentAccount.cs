using System.ComponentModel.DataAnnotations;

namespace SoccerSchool.Api.Domain;

public class ParentAccount
{
    public int Id { get; set; }

    [Required, MaxLength(450)]
    public string UserId { get; set; } = string.Empty;
    public ApplicationUser? User { get; set; }

    [Required, MaxLength(80)]
    public string FirstName { get; set; } = string.Empty;

    [Required, MaxLength(80)]
    public string LastName { get; set; } = string.Empty;

    [MaxLength(200)]
    public string? AddressLine1 { get; set; }

    [MaxLength(200)]
    public string? AddressLine2 { get; set; }

    [MaxLength(80)]
    public string? City { get; set; }

    [MaxLength(40)]
    public string? State { get; set; } = "NV";

    [MaxLength(20)]
    public string? PostalCode { get; set; }

    [MaxLength(32)]
    public string? CellPhone { get; set; }

    public Language Language { get; set; } = Language.English;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public List<Player> Players { get; set; } = new();
    public List<Registration> Registrations { get; set; } = new();
}
