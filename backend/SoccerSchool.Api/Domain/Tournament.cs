using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SoccerSchool.Api.Domain;

/// <summary>
/// A tournament the admin runs end-to-end: name, dates, total cost the school is paying, and the
/// per-player cost charged to families. After creating a tournament the admin builds a dedicated
/// team for it (a brand-new Team with its own roster, separate from regular season teams), then
/// sends a WhatsApp confirmation per rostered player asking the family to confirm attendance.
///
/// Legacy GotSport sync still works for existing rows — <see cref="GotSportEventId"/> and
/// <see cref="GotSportTeamId"/> stay around so historical tournaments keep importing their games.
/// New tournaments leave those at 0.
/// </summary>
public class Tournament
{
    public int Id { get; set; }

    [Required, MaxLength(128)]
    public string Name { get; set; } = string.Empty;

    /// <summary>Inclusive first day of the tournament. Used as half of the formatted date string
    /// the WhatsApp template renders as parameter 1.</summary>
    public DateOnly? StartDate { get; set; }

    /// <summary>Inclusive last day of the tournament. May equal <see cref="StartDate"/> for a
    /// single-day event.</summary>
    public DateOnly? EndDate { get; set; }

    /// <summary>What LVSS is paying to enter the tournament — for admin tracking only. Nullable
    /// because legacy GotSport-only tournaments don't have it.</summary>
    [Column(TypeName = "decimal(10,2)")]
    public decimal? TotalCost { get; set; }

    /// <summary>Cost passed to each family — surfaces in the confirmation message body as
    /// template parameter 3.</summary>
    [Column(TypeName = "decimal(10,2)")]
    public decimal? CostPerPlayer { get; set; }

    /// <summary>Dedicated team built for this tournament. Nullable because the workflow creates
    /// the tournament first and then the team. Once the team exists this is the canonical roster
    /// the send-confirmations + attendance flows iterate over.</summary>
    public int? TeamId { get; set; }
    public Team? Team { get; set; }

    /// <summary>GotSport event ID (the number in <c>/org_event/events/{N}/schedules</c>). Legacy
    /// field — only meaningful for tournaments imported from GotSport.</summary>
    public int GotSportEventId { get; set; }

    /// <summary>GotSport team ID. Legacy.</summary>
    public int GotSportTeamId { get; set; }

    public DateTime? LastSyncedAt { get; set; }

    [MaxLength(512)]
    public string? LastSyncMessage { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public List<ScheduledGame> Games { get; set; } = new();

    /// <summary>One row per rostered player tracking their tournament confirmation. Populated
    /// either by the admin clicking Confirm/Decline/Maybe, or by the webhook parsing inbound
    /// WhatsApp replies routed back to a broadcast that carries this tournament's id.</summary>
    public List<TournamentAttendance> Attendances { get; set; } = new();

    /// <summary>Multi-team participation. New code path: a tournament has many teams and each
    /// team carries its own GotSport sync state via <see cref="TournamentTeam"/>. Legacy single-
    /// team tournaments (those that pre-date this collection) are auto-migrated into one row
    /// here on deploy, so this is the canonical list going forward.</summary>
    public List<TournamentTeam> Teams { get; set; } = new();
}
