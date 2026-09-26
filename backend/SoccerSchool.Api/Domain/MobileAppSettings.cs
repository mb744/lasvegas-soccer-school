using System.ComponentModel.DataAnnotations;

namespace SoccerSchool.Api.Domain;

/// <summary>
/// Singleton-row settings for the parent app (<see cref="Id"/> is always 1; created on first save).
/// Lets an admin force an update without a redeploy.
/// </summary>
public class MobileAppSettings
{
    public int Id { get; set; } = 1;

    /// <summary>Oldest app version still allowed, e.g. "1.4.0". Older installs get a full-screen
    /// "please update" they can't dismiss. Null = no minimum.</summary>
    [MaxLength(32)]
    public string? MinimumVersion { get; set; }

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
