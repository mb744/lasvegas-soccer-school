namespace SoccerSchool.Api.Domain;

/// <summary>Whether a rostered player is coming to a specific event.</summary>
public enum AttendanceStatus
{
    Pending = 0,
    Confirmed = 1,
    Declined = 2,
    Maybe = 3,
}

/// <summary>How a status was set — so an auto-parsed reply never clobbers an admin's manual call.</summary>
public enum AttendanceSource
{
    ParentReply = 0,
    Admin = 1,
}

/// <summary>
/// One rostered player's confirmation status for one event. Created on demand: a player with no
/// row is treated as <see cref="AttendanceStatus.Pending"/>. Rows are set either by parsing a
/// parent's inbound reply (<see cref="AttendanceSource.ParentReply"/>) or by the admin clicking in
/// the schedule UI (<see cref="AttendanceSource.Admin"/>).
/// </summary>
public class EventAttendance
{
    public int Id { get; set; }

    public int ScheduledGameId { get; set; }
    public ScheduledGame? ScheduledGame { get; set; }

    public int PlayerId { get; set; }
    public Player? Player { get; set; }

    public AttendanceStatus Status { get; set; } = AttendanceStatus.Pending;

    public AttendanceSource Source { get; set; } = AttendanceSource.ParentReply;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
