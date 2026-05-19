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

public record ScheduleSyncResultDto(bool Success, int Added, int Updated, string Message);
