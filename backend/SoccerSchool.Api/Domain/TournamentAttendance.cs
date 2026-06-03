namespace SoccerSchool.Api.Domain;

/// <summary>
/// One rostered player's confirmation status for one tournament — mirror of
/// <see cref="EventAttendance"/> but keyed by tournament instead of a specific scheduled game.
/// Reuses the same <see cref="AttendanceStatus"/> and <see cref="AttendanceSource"/> enums so
/// the UI and webhook reply parser stay symmetrical with event attendance.
/// </summary>
public class TournamentAttendance
{
    public int Id { get; set; }

    public int TournamentId { get; set; }
    public Tournament? Tournament { get; set; }

    public int PlayerId { get; set; }
    public Player? Player { get; set; }

    public AttendanceStatus Status { get; set; } = AttendanceStatus.Pending;

    public AttendanceSource Source { get; set; } = AttendanceSource.ParentReply;

    /// <summary>Whether this player has paid the per-player tournament/league fee. Drives the
    /// `tournamentfee_*`/`leaguefee_*` reminder fan-out — only players with Paid=false are
    /// targeted. Toggled from the per-team attendance table by an admin.</summary>
    public bool Paid { get; set; }

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
