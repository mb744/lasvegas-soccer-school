using System.ComponentModel.DataAnnotations;

namespace SoccerSchool.Api.Domain;

/// <summary>
/// One playable surface under a <see cref="Venue"/> — "Field 1", "North Field", "Court B" etc.
/// Optional per-venue catalog: venues that only have a single playing area can skip creating
/// any fields and continue using the free-text location string. When fields exist, hosted-
/// tournament scheduling can assign specific matches to specific fields.
/// </summary>
public class VenueField
{
    public int Id { get; set; }

    public int VenueId { get; set; }
    public Venue? Venue { get; set; }

    [Required, MaxLength(80)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? Notes { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
