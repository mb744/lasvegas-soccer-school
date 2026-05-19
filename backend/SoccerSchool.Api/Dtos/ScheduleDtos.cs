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
    bool? IsHome);

public record SavePracticeRequest
{
    public DateTime StartsAt { get; init; }

    public DateTime? EndsAt { get; init; }

    [MaxLength(512)]
    public string? Location { get; init; }

    [MaxLength(512)]
    public string? Summary { get; init; }
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

public record ScheduleSyncResultDto(bool Success, int Added, int Updated, string Message);
