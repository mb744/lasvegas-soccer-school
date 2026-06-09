using System.ComponentModel.DataAnnotations;

namespace SoccerSchool.Api.Domain;

/// <summary>
/// A park / field / venue the admin manages in Settings: name, street address, and playing surface.
/// Events reference a venue via <see cref="ScheduledGame.VenueId"/> as the structured "where". The
/// free-text <see cref="ScheduledGame.Location"/> stays as optional extra detail (e.g. "field 3")
/// and as the value synced games carry from GotSport/TeamSnap.
/// </summary>
public class Venue
{
    public int Id { get; set; }

    [Required, MaxLength(128)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(512)]
    public string? Address { get; set; }

    public SurfaceType Surface { get; set; } = SurfaceType.Unspecified;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
