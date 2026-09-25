namespace SoccerSchool.Api.Domain;

/// <summary>
/// A kid tapped "I did it!" on a drill for a given day. Unique per (player, drill, date) so a drill
/// that stays assigned for a week can be completed once each day. Drives streaks and progress.
/// </summary>
public class DrillCompletion
{
    public int Id { get; set; }

    public int PlayerId { get; set; }
    public Player? Player { get; set; }

    public int DrillId { get; set; }
    public Drill? Drill { get; set; }

    /// <summary>The club-local calendar day the drill was done for.</summary>
    public DateOnly Date { get; set; }

    public DateTime CompletedAt { get; set; } = DateTime.UtcNow;
}
