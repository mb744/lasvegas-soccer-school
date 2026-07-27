using System.ComponentModel.DataAnnotations;

namespace SoccerSchool.Api.Domain;

/// <summary>
/// A tournament or league LVSS is HOSTING — distinct from <see cref="Tournament"/> which tracks
/// LVSS teams travelling to external tournaments. Admin creates a hosted event, picks a venue,
/// sets a per-team fee, and rosters both LVSS teams and external <see cref="InvitedTeam"/>s
/// via <see cref="HostedTournamentTeam"/> join rows.
/// </summary>
public class HostedTournament
{
    public int Id { get; set; }

    [Required, MaxLength(160)]
    public string Name { get; set; } = string.Empty;

    /// <summary>Same enum as the participating-tournament flow — Tournament (one weekend) vs
    /// League (multi-week season). Drives labeling only.</summary>
    public TournamentKind Kind { get; set; } = TournamentKind.Tournament;

    public DateOnly StartDate { get; set; }

    /// <summary>Single-day events leave this null.</summary>
    public DateOnly? EndDate { get; set; }

    /// <summary>Optional link to a curated venue for one-click address + Maps link. When null the
    /// free-text <see cref="Location"/> field is used.</summary>
    public int? VenueId { get; set; }
    public Venue? Venue { get; set; }

    /// <summary>Free-text location — used when no <see cref="Venue"/> is picked. Handy for pop-up
    /// or partner venues that aren't worth adding to the venue catalog.</summary>
    [MaxLength(400)]
    public string? Location { get; set; }

    /// <summary>Fee each participating team pays LVSS. Optional (some scrimmage events are free).</summary>
    public decimal? CostPerTeam { get; set; }

    /// <summary>Admin-only notes — not shown to participating teams.</summary>
    [MaxLength(2000)]
    public string? Notes { get; set; }

    /// <summary>Rich body copy for the "here are the rules + schedule" email. Rendered above
    /// the schedule table on the public schedule page too, so parents/coaches see the same text.</summary>
    public string? RulesOfPlay { get; set; }

    /// <summary>URL slug for the public schedule page (/tournament/{slug}). Generated on
    /// create; admin can regenerate to invalidate an old link.</summary>
    [MaxLength(64)]
    public string? PublicSlug { get; set; }

    /// <summary>Default per-match window length in minutes. The scheduler advances the slot
    /// cursor by this amount for each field. Editable per match afterward. Kept for backwards
    /// compat with existing rows — new rows should derive it from Half + Halftime + between.</summary>
    public int MatchDurationMinutes { get; set; } = 60;

    /// <summary>Length of one half in minutes. Two halves plus <see cref="HalftimeMinutes"/>
    /// make up a single match; the scheduler uses that sum as the play window.</summary>
    public int HalfMinutes { get; set; } = 25;

    /// <summary>Break between the two halves.</summary>
    public int HalftimeMinutes { get; set; } = 5;

    /// <summary>Gap the scheduler leaves between back-to-back matches on the same field so
    /// referees / teams have transition time.</summary>
    public int MinutesBetweenGames { get; set; } = 5;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public List<HostedTournamentTeam> Teams { get; set; } = new();
    public List<HostedTournamentTier> Tiers { get; set; } = new();
    public List<HostedTournamentDay> Days { get; set; } = new();
    public List<HostedTournamentField> Fields { get; set; } = new();
    public List<HostedTournamentMatch> Matches { get; set; } = new();
}
