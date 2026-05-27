using System.ComponentModel.DataAnnotations;

namespace SoccerSchool.Api.Domain;

/// <summary>
/// Admin-managed age bracket (e.g. "U10", "U12") with a DOB date range. Used to auto-tag each
/// player on a registration with the right age group based on their birthdate. Both endpoints
/// of <see cref="DobStart"/>/<see cref="DobEnd"/> are inclusive.
/// </summary>
public class AgeClassification
{
    public int Id { get; set; }

    [Required, MaxLength(64)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(256)]
    public string? Description { get; set; }

    /// <summary>Earliest DOB (inclusive) that maps to this classification. Older players need a
    /// classification covering an earlier date range.</summary>
    public DateOnly DobStart { get; set; }

    /// <summary>Latest DOB (inclusive) that maps to this classification.</summary>
    public DateOnly DobEnd { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
