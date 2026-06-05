using System.ComponentModel.DataAnnotations;
using SoccerSchool.Api.Domain;

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
    int GotSportEventId,
    int GotSportTeamId,
    DateTime? LastSyncedAt,
    string? LastSyncMessage,
    string? CoachName,
    string? CoachEmail,
    string? CoachPhone,
    DateTime CreatedAt,
    IReadOnlyList<RosterMemberDto> Roster,
    IReadOnlyList<ScheduledGameDto> UpcomingGames,
    IReadOnlyList<TeamCoachDto> Coaches);

/// <summary>One coach on a team. Carries enough to route messaging per-coach (language +
/// HasWhatsApp) and to render an admin edit row (name + email + phone).</summary>
public record TeamCoachDto(
    int Id,
    int TeamId,
    string Name,
    string? Email,
    string? Phone,
    Language Language,
    bool HasWhatsApp,
    /// <summary>FK to the admin Coaches roster when this row was picked from the directory;
    /// null when the admin typed the coach in by hand on the team card. Lets the UI show
    /// a "View coach profile" link.</summary>
    int? CoachId,
    /// <summary>Head vs assistant. Drives the role badge in the per-team coach editor.</summary>
    TeamCoachRole Role,
    DateTime CreatedAt);

public record AddTeamCoachRequest
{
    [Required, MaxLength(160)] public string Name { get; init; } = string.Empty;
    [MaxLength(256)] public string? Email { get; init; }
    [MaxLength(32)] public string? Phone { get; init; }
    public Language Language { get; init; } = Language.English;
    public bool HasWhatsApp { get; init; }
    /// <summary>Optional: pick from the admin Coaches roster. When set, the controller pulls
    /// name/email/phone/language/HasWhatsApp from the Coach record and ignores the values in
    /// this request — the pick is authoritative.</summary>
    public int? CoachId { get; init; }
    public TeamCoachRole Role { get; init; } = TeamCoachRole.HeadCoach;
}

public record UpdateTeamCoachRequest
{
    [Required, MaxLength(160)] public string Name { get; init; } = string.Empty;
    [MaxLength(256)] public string? Email { get; init; }
    [MaxLength(32)] public string? Phone { get; init; }
    public Language Language { get; init; } = Language.English;
    public bool HasWhatsApp { get; init; }
    /// <summary>Optional rebind to a Coach profile. Same precedence rule as Add: when set,
    /// the controller pulls contact details from the Coach record.</summary>
    public int? CoachId { get; init; }
    public TeamCoachRole Role { get; init; } = TeamCoachRole.HeadCoach;
}

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

/// <summary>Coach contact for a team. All optional; the phone is E.164-normalized on save.</summary>
public record SaveCoachRequest
{
    [MaxLength(160)] public string? CoachName { get; init; }
    [MaxLength(256)] public string? CoachEmail { get; init; }
    [MaxLength(32)] public string? CoachPhone { get; init; }
}
