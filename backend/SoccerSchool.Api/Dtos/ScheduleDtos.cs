using System.ComponentModel.DataAnnotations;
using SoccerSchool.Api.Domain;

namespace SoccerSchool.Api.Dtos;

public record SaveTeamRequest
{
    [Required, MaxLength(128)]
    public string Name { get; init; } = string.Empty;

    /// <summary>Either set EventId + TeamId directly, or paste the full schedule URL into
    /// <see cref="ScheduleUrl"/> and the server will parse the IDs out of it.</summary>
    public int? GotSportEventId { get; init; }
    public int? GotSportTeamId { get; init; }

    /// <summary>Convenience: paste the public schedule URL
    /// (https://system.gotsport.com/org_event/events/{eventId}/schedules?team={teamId})
    /// and the server extracts the IDs.</summary>
    [MaxLength(1024)]
    public string? ScheduleUrl { get; init; }

    /// <summary>Optional curated message group to default-target when picking a game from this team.</summary>
    public int? MessageGroupId { get; init; }
}

public record TeamSummary(
    int Id,
    string Name,
    int GotSportEventId,
    int GotSportTeamId,
    int? MessageGroupId,
    string? MessageGroupName,
    DateTime? LastSyncedAt,
    string? LastSyncMessage,
    int UpcomingGameCount,
    DateTime CreatedAt);

public record TeamDetail(
    int Id,
    string Name,
    int GotSportEventId,
    int GotSportTeamId,
    int? MessageGroupId,
    string? MessageGroupName,
    DateTime? LastSyncedAt,
    string? LastSyncMessage,
    DateTime CreatedAt,
    IReadOnlyList<ScheduledGameDto> UpcomingGames);

public record ScheduledGameDto(
    int Id,
    int TeamId,
    string TeamName,
    int? MessageGroupId,
    string? MessageGroupName,
    ScheduledEventKind Kind,
    DateTime StartsAt,
    DateTime? EndsAt,
    string? Summary,
    string? Location,
    string? Description,
    string? OpponentName,
    bool? IsHome,
    Guid? SeriesId,
    bool IsCancelled,
    DateTime? CancelledAt,
    int? TournamentId,
    string? TournamentName);

public record SavePracticeRequest
{
    public DateTime StartsAt { get; init; }

    public DateTime? EndsAt { get; init; }

    [MaxLength(512)]
    public string? Location { get; init; }

    [MaxLength(512)]
    public string? Summary { get; init; }
}

/// <summary>Admin-entered game (manual; not scraped from GotSport). Lives in the same
/// ScheduledGames table with Kind=Game and a synthesized ExternalUid (`manual-game-{guid}`)
/// so the unique (team, uid) index doesn't collide with scraped games.</summary>
public record SaveGameRequest
{
    public DateTime StartsAt { get; init; }

    public DateTime? EndsAt { get; init; }

    [MaxLength(256)]
    public string? OpponentName { get; init; }

    /// <summary>true = we're home, false = away, null = unknown.</summary>
    public bool? IsHome { get; init; }

    [MaxLength(512)]
    public string? Location { get; init; }

    [MaxLength(512)]
    public string? Summary { get; init; }

    /// <summary>Optional tournament this game belongs to (set when added from the Tournaments tab).</summary>
    public int? TournamentId { get; init; }
}

/// <summary>Create a recurring practice series. Each combination of (day-of-week × occurrence date
/// in the [StartDate, EndDate] range) materializes as its own ScheduledGame row sharing a SeriesId.</summary>
public record SavePracticeSeriesRequest
{
    /// <summary>First date the series may occur on (inclusive, local date).</summary>
    public DateTime StartDate { get; init; }

    /// <summary>Last date the series may occur on (inclusive, local date).</summary>
    public DateTime EndDate { get; init; }

    /// <summary>Local time-of-day each occurrence starts at, in HH:mm 24-hour format
    /// (e.g. "17:00" for 5pm). Combined with each matching date to form StartsAt.</summary>
    [Required]
    public string StartTime { get; init; } = "17:00";

    /// <summary>Optional local end time-of-day, same HH:mm format.</summary>
    public string? EndTime { get; init; }

    /// <summary>Which days of the week the practice happens, 0 = Sunday … 6 = Saturday
    /// (matches <see cref="DayOfWeek"/>). At least one required.</summary>
    public int[] DaysOfWeek { get; init; } = Array.Empty<int>();

    [MaxLength(512)]
    public string? Location { get; init; }

    [MaxLength(512)]
    public string? Summary { get; init; }
}

public record PracticeSeriesCreatedDto(Guid SeriesId, int Count, IReadOnlyList<ScheduledGameDto> Occurrences);

/// <summary>One previously-notified parent for the cancel-notification flow. Sourced from the
/// BroadcastRecipient rows of any past broadcasts that referenced the cancelled event.</summary>
public record EventRecipientDto(string Phone, string? Name, Language Language);

public record ScheduleSyncResultDto(bool Success, int Added, int Updated, string Message);

// --- Event attendance (per rostered player confirmation) ---

/// <summary>One rostered player's confirmation status for an event. <see cref="Source"/> is 1 when
/// an admin set it manually, 0 when it came from a parent's reply. <see cref="UpdatedAt"/> is null
/// when there's no stored row yet (player defaults to Pending).</summary>
public record EventAttendanceDto(
    int PlayerId,
    string FirstName,
    string LastName,
    string? ParentName,
    string? ParentPhone,
    AttendanceStatus Status,
    AttendanceSource Source,
    DateTime? UpdatedAt);

public record EventAttendanceListDto(
    int EventId,
    int Confirmed,
    int Declined,
    int Maybe,
    int Pending,
    IReadOnlyList<EventAttendanceDto> Items);

/// <summary>Per-event confirmation counts for a team, used to badge each schedule row.</summary>
public record EventAttendanceSummaryDto(
    int EventId,
    int Confirmed,
    int Declined,
    int Maybe,
    int Pending);

public record SetAttendanceRequest
{
    public AttendanceStatus Status { get; init; }
}

// --- Tournaments (a team's GotSport competition entry) ---

public record TournamentSummary(
    int Id,
    string Name,
    int TeamId,
    string TeamName,
    int GotSportEventId,
    int GotSportTeamId,
    DateTime? LastSyncedAt,
    string? LastSyncMessage,
    int GameCount,
    int UpcomingGameCount,
    DateTime CreatedAt);

public record SaveTournamentRequest
{
    [Required, MaxLength(128)]
    public string Name { get; init; } = string.Empty;

    public int TeamId { get; init; }

    /// <summary>Either set EventId + TeamId directly, or paste the schedule URL and the server
    /// parses them out (same parsing the old team form used).</summary>
    public int? GotSportEventId { get; init; }
    public int? GotSportTeamId { get; init; }

    [MaxLength(1024)]
    public string? ScheduleUrl { get; init; }
}
