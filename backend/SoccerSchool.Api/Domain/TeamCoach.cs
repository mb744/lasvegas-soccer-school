using System.ComponentModel.DataAnnotations;

namespace SoccerSchool.Api.Domain;

/// <summary>
/// One coach on a <see cref="Team"/>. Replaces the original single-coach inline fields on Team
/// (CoachName/Email/Phone) so a team can have multiple coaches — head coach, assistants, team
/// manager, etc. Each row carries its own language preference and HasWhatsApp flag so the
/// recipient resolver can route per-coach the same way it does per-parent.
/// </summary>
public class TeamCoach
{
    public int Id { get; set; }

    public int TeamId { get; set; }
    public Team? Team { get; set; }

    [Required, MaxLength(160)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(256)]
    public string? Email { get; set; }

    /// <summary>E.164-normalized on write, mirroring parent cell numbers.</summary>
    [MaxLength(32)]
    public string? Phone { get; set; }

    public Language Language { get; set; } = Language.English;

    public bool HasWhatsApp { get; set; }

    /// <summary>Optional link back to the admin Coaches roster (the HR-style profile that
    /// holds mailing address, monthly stipend, certifications). When set, the row is a "pick"
    /// from the roster; when null, the admin typed the contact in directly without binding it
    /// to a coach profile. Null doesn't break anything — the row still routes for messaging.</summary>
    public int? CoachId { get; set; }
    public Coach? Coach { get; set; }

    /// <summary>Head vs assistant coach. Surfaced as a badge on the per-team coach editor;
    /// future routing rules (e.g. head coach gets the schedule-change ping but not the
    /// "we need a sub" thread) can branch on this.</summary>
    public TeamCoachRole Role { get; set; } = TeamCoachRole.HeadCoach;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
