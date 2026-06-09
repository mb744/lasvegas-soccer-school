using System.ComponentModel.DataAnnotations;

namespace SoccerSchool.Api.Domain;

/// <summary>
/// A club-wide uniform/kit the admin manages in Settings: a name plus shirt/shorts/sock colors,
/// and an optional <see cref="UniformDesignation"/> marking it as the default Home, Away, or
/// Practice kit. Games reference a uniform via <see cref="ScheduledGame.UniformId"/>; when a game
/// leaves that null, the send/display layer falls back to the uniform whose designation matches the
/// game's home/away setting. Replaces the old hardcoded "white jersey, blue shorts, blue socks" /
/// "all blue" wear-text constants.
/// </summary>
public class Uniform
{
    public int Id { get; set; }

    [Required, MaxLength(128)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(64)]
    public string? ShirtColor { get; set; }

    [MaxLength(64)]
    public string? ShortsColor { get; set; }

    [MaxLength(64)]
    public string? SockColor { get; set; }

    /// <summary>Home / Away / Practice default, or None. At most one uniform per non-None value.</summary>
    public UniformDesignation Designation { get; set; } = UniformDesignation.None;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>Renders the kit as wear text, e.g. "white jersey, blue shorts, blue socks". Falls
    /// back to the name when no colors are set.</summary>
    public string ToWearText()
    {
        var parts = new List<string>(3);
        if (!string.IsNullOrWhiteSpace(ShirtColor)) parts.Add($"{ShirtColor.Trim()} jersey");
        if (!string.IsNullOrWhiteSpace(ShortsColor)) parts.Add($"{ShortsColor.Trim()} shorts");
        if (!string.IsNullOrWhiteSpace(SockColor)) parts.Add($"{SockColor.Trim()} socks");
        return parts.Count > 0 ? string.Join(", ", parts) : Name;
    }
}
