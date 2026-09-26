using System.ComponentModel.DataAnnotations;

namespace SoccerSchool.Api.Domain;

/// <summary>
/// Declared in the order drills appear in a kid's daily plan: warm up first, cool down last.
/// </summary>
public enum DrillCategory
{
    Fitness = 0,
    BallMastery = 1,
    Dribbling = 2,
    Passing = 3,
    Shooting = 4,
    Stretching = 5,
}

/// <summary>
/// An at-home training activity authored by an admin, in English and Spanish, for the Daily
/// Training app. Assigned to players via <see cref="DrillAssignment"/>.
/// </summary>
public class Drill
{
    public int Id { get; set; }

    public DrillCategory Category { get; set; }

    [Required, MaxLength(120)]
    public string TitleEn { get; set; } = string.Empty;

    [Required, MaxLength(120)]
    public string TitleEs { get; set; } = string.Empty;

    [MaxLength(1000)]
    public string DescriptionEn { get; set; } = string.Empty;

    [MaxLength(1000)]
    public string DescriptionEs { get; set; } = string.Empty;

    /// <summary>One step per line. Kept as plain text so admins edit it in a single textarea; the
    /// training API splits it into a list and pairs English/Spanish lines by position.</summary>
    [MaxLength(4000)]
    public string StepsEn { get; set; } = string.Empty;

    [MaxLength(4000)]
    public string StepsEs { get; set; } = string.Empty;

    public int DurationMinutes { get; set; }

    /// <summary>Optional rep target ("50 toe taps"). Null for purely timed drills.</summary>
    public int? Reps { get; set; }

    [MaxLength(512)]
    public string? VideoUrl { get; set; }

    /// <summary>Archived drills stay in history (completions) but drop out of every kid's plan.</summary>
    public bool IsActive { get; set; } = true;

    /// <summary>Identity user id of whoever authored the drill (admin or coach). A coach may edit,
    /// archive or delete only drills they created; admins may edit any. Audit-style, no FK — the
    /// drill outlives its author's account. Null for drills created before authorship was tracked
    /// (admin-only to edit).</summary>
    [MaxLength(450)]
    public string? CreatedByUserId { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public List<DrillAssignment> Assignments { get; set; } = new();
}
