using System.ComponentModel.DataAnnotations;

namespace SoccerSchool.Api.Dtos;

/// <summary>List-row view of a team for the roster-builder admin card. <see cref="GotSportLinked"/>
/// is true when the team has GotSport IDs set (i.e. schedule sync is available).</summary>
public record RosterTeamSummary(
    int Id,
    string Name,
    int RosterCount,
    int UpcomingGameCount,
    bool GotSportLinked,
    int? MessageGroupId,
    string? MessageGroupName,
    DateTime CreatedAt);

/// <summary>A team plus its roster and upcoming events. Reuses <see cref="ScheduledGameDto"/> for
/// the schedule section so the new card can render practices/games the same way the schedule UI does.</summary>
public record RosterTeamDetail(
    int Id,
    string Name,
    int? MessageGroupId,
    string? MessageGroupName,
    bool GotSportLinked,
    DateTime CreatedAt,
    IReadOnlyList<RosterMemberDto> Roster,
    IReadOnlyList<ScheduledGameDto> UpcomingGames);

/// <summary>One roster player. Age bracket / parent contact are pulled from the player's
/// most-recent registration for display; null when the player has no registration on file.</summary>
public record RosterMemberDto(
    int PlayerId,
    string FirstName,
    string LastName,
    DateOnly DateOfBirth,
    string? AgeBracket,
    string? ParentName,
    string? ParentPhone,
    string? ParentEmail,
    DateTime AddedAt);

/// <summary>A registered player who can be added to a team's roster (not already on it).</summary>
public record AvailablePlayerDto(
    int PlayerId,
    string FirstName,
    string LastName,
    DateOnly DateOfBirth,
    string? AgeBracket,
    string? ParentName);

public record CreateRosterTeamRequest
{
    [Required, MaxLength(128)]
    public string Name { get; init; } = string.Empty;
}

public record RenameTeamRequest
{
    [Required, MaxLength(128)]
    public string Name { get; init; } = string.Empty;
}

public record AddRosterMembersRequest
{
    public int[] PlayerIds { get; init; } = Array.Empty<int>();
}
