using System.ComponentModel.DataAnnotations;

namespace SoccerSchool.Api.Domain;

/// <summary>
/// A short message an admin publishes to parents through the mobile app's Home tab. Scoped either
/// school-wide (<see cref="TeamId"/> null) or to a specific team; a parent sees announcements
/// where the team matches one of their kids' rosters plus every school-wide entry. Announcements
/// remain visible until <see cref="EndsAt"/> passes (null = indefinite) or an admin flips
/// <see cref="IsActive"/> off.
/// </summary>
public class Announcement
{
    public int Id { get; set; }

    [Required, MaxLength(200)]
    public string Title { get; set; } = string.Empty;

    [Required, MaxLength(2000)]
    public string Body { get; set; } = string.Empty;

    /// <summary>Optional target team. When null the announcement is school-wide and every parent
    /// with a signed-in mobile session sees it.</summary>
    public int? TeamId { get; set; }
    public Team? Team { get; set; }

    /// <summary>When the announcement stops appearing on parents' Home tabs. Null = indefinite
    /// (admin must flip <see cref="IsActive"/> off to retract it).</summary>
    public DateTime? EndsAt { get; set; }

    /// <summary>Admin off-switch — lets an announcement be hidden without losing its history.
    /// The mobile query filters on this in addition to <see cref="EndsAt"/>.</summary>
    public bool IsActive { get; set; } = true;

    /// <summary>Login that authored the announcement — for the admin activity log. Null when the
    /// creating user was later deleted; the announcement itself stays.</summary>
    [MaxLength(450)]
    public string? CreatedByUserId { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
