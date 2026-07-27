using System.ComponentModel.DataAnnotations;

namespace SoccerSchool.Api.Domain;

/// <summary>
/// One scheduled match at a hosted tournament. Produced by the schedule generator, then editable
/// by the admin (drag/reassign time, field, teams). TeamA / TeamB reference
/// <see cref="HostedTournamentTeam"/> so the scheduler can slot both LVSS and invited teams
/// uniformly. All FKs use ClientSetNull semantics so removing a team, bracket, field, or day
/// leaves an "orphan" match the admin can re-slot rather than silently dropping matches.
/// </summary>
public class HostedTournamentMatch
{
    public int Id { get; set; }

    public int HostedTournamentId { get; set; }
    public HostedTournament? HostedTournament { get; set; }

    /// <summary>Tier the match belongs to (derivable from either team's bracket, cached here for
    /// quick filtering). Null when the match was created ad-hoc without a tier.</summary>
    public int? TierId { get; set; }
    public HostedTournamentTier? Tier { get; set; }

    public int? TeamAId { get; set; }
    public HostedTournamentTeam? TeamA { get; set; }

    public int? TeamBId { get; set; }
    public HostedTournamentTeam? TeamB { get; set; }

    public int? FieldId { get; set; }
    public HostedTournamentField? Field { get; set; }

    public int? DayId { get; set; }
    public HostedTournamentDay? Day { get; set; }

    /// <summary>Actual kickoff time inside the day. Null when unscheduled.</summary>
    public TimeOnly? StartTime { get; set; }

    /// <summary>Team A's final score. Null when the match hasn't been played yet — either both
    /// scores are set (the match counts toward standings) or both are null (still pending). The
    /// admin flips these on the Schedule page as results come in; standings recompute on load.</summary>
    public int? TeamAScore { get; set; }

    /// <summary>Team B's final score. Same semantics as <see cref="TeamAScore"/>.</summary>
    public int? TeamBScore { get; set; }

    /// <summary>Length of the match window in minutes; drives the next-slot cursor when
    /// scheduling. Defaults to the event's <see cref="HostedTournament.MatchDurationMinutes"/>.</summary>
    public int DurationMinutes { get; set; } = 60;

    /// <summary>Score / result / referee note — freeform, admin-editable after the match.</summary>
    [MaxLength(500)]
    public string? Notes { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
