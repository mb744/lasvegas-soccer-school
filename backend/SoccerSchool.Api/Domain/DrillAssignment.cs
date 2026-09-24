using System.ComponentModel.DataAnnotations;

namespace SoccerSchool.Api.Domain;

public enum DrillTargetType
{
    Player = 0,
    Team = 1,
    AgeClassification = 2,
}

/// <summary>
/// Puts a <see cref="Drill"/> on kids' daily plans for a date range. Targets exactly one of: a
/// single player, every player rostered on a team, or every player in an age classification
/// (resolved from their active-season registration, falling back to date of birth). A kid's plan
/// for a day is the de-duplicated union of every assignment that reaches them.
/// </summary>
public class DrillAssignment
{
    public int Id { get; set; }

    public int DrillId { get; set; }
    public Drill? Drill { get; set; }

    public DrillTargetType TargetType { get; set; }

    // Exactly one of these is set, matching TargetType (enforced by a check constraint).
    public int? PlayerId { get; set; }
    public Player? Player { get; set; }

    public int? TeamId { get; set; }
    public Team? Team { get; set; }

    public int? AgeClassificationId { get; set; }
    public AgeClassification? AgeClassification { get; set; }

    /// <summary>First day (club time zone) the drill shows up, inclusive.</summary>
    public DateOnly StartDate { get; set; }

    /// <summary>Last day, inclusive. Null = open-ended.</summary>
    public DateOnly? EndDate { get; set; }

    /// <summary>Identity user id of the admin who made the assignment (audit only, no FK).</summary>
    [MaxLength(450)]
    public string? CreatedByUserId { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
