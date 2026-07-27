using System.ComponentModel.DataAnnotations;
using SoccerSchool.Api.Domain;

namespace SoccerSchool.Api.Dtos;

// ============================================================================
// InvitedTeam — external teams catalog
// ============================================================================

public record InvitedTeamDto(
    int Id,
    string Name,
    string? HeadCoachName,
    string? HeadCoachPhone,
    string? HeadCoachEmail,
    string? AgeGroup,
    string? Notes,
    DateTime CreatedAt,
    DateTime UpdatedAt);

public record SaveInvitedTeamRequest
{
    [Required, MaxLength(160)] public string Name { get; init; } = string.Empty;
    [MaxLength(160)] public string? HeadCoachName { get; init; }
    [MaxLength(32)] public string? HeadCoachPhone { get; init; }
    [MaxLength(320)] public string? HeadCoachEmail { get; init; }
    [MaxLength(60)] public string? AgeGroup { get; init; }
    [MaxLength(2000)] public string? Notes { get; init; }
}

// ============================================================================
// HostedTournament — events LVSS is hosting
// ============================================================================

/// <summary>One team row on a hosted tournament — either an LVSS team or an external one,
/// with the label + age-group hint already resolved so the frontend doesn't have to join.</summary>
public record HostedTournamentTeamDto(
    int Id,
    int? LvssTeamId,
    string? LvssTeamName,
    int? InvitedTeamId,
    string? InvitedTeamName,
    string? AgeGroup,
    string? HeadCoachName,
    string? HeadCoachPhone,
    string? HeadCoachEmail,
    string? Notes,
    int? TierId,
    string? TierName,
    int? BracketId,
    string? BracketName,
    bool Paid,
    DateTime? PaidAt,
    string? PaymentMethod,
    string? PaymentReference,
    DateTime CreatedAt);

/// <summary>Named bracket/division inside a hosted event.</summary>
public record HostedTournamentTierDto(
    int Id,
    string Name,
    int SortOrder,
    string? Notes,
    bool CrossBracketPlay,
    DateTime CreatedAt,
    IReadOnlyList<HostedTournamentBracketDto> Brackets);

public record HostedTournamentBracketDto(
    int Id,
    int TierId,
    string Name,
    int SortOrder,
    string? Notes,
    DateTime CreatedAt);

public record HostedTournamentFieldDto(
    int Id,
    int? VenueFieldId,
    string Name,
    int SortOrder,
    string? Notes,
    DateTime CreatedAt);

public record HostedTournamentMatchDto(
    int Id,
    int? TierId,
    string? TierName,
    int? TeamAId,
    string? TeamALabel,
    int? TeamBId,
    string? TeamBLabel,
    int? FieldId,
    string? FieldName,
    int? DayId,
    DateOnly? DayDate,
    TimeOnly? StartTime,
    int DurationMinutes,
    string? Notes);

public record SaveHostedTournamentBracketRequest
{
    [Required, MaxLength(80)] public string Name { get; init; } = string.Empty;
    public int SortOrder { get; init; }
    [MaxLength(500)] public string? Notes { get; init; }
}

public record SaveHostedTournamentFieldRequest
{
    [Required, MaxLength(80)] public string Name { get; init; } = string.Empty;
    public int? VenueFieldId { get; init; }
    public int SortOrder { get; init; }
    [MaxLength(500)] public string? Notes { get; init; }
}

public record AssignTeamBracketRequest
{
    /// <summary>Null clears the bracket assignment.</summary>
    public int? BracketId { get; init; }
}

public record UpdateTierFlagsRequest
{
    public bool CrossBracketPlay { get; init; }
}

public record SendScheduleEmailRequest
{
    /// <summary>Subject override; falls back to "{Event name} — Schedule" when null.</summary>
    [MaxLength(256)] public string? Subject { get; init; }
    /// <summary>Extra copy the admin can layer above the rules body. Optional.</summary>
    public string? Intro { get; init; }
}

public record SendScheduleEmailResult(int Sent, int Skipped, string? Message);

public record GenerateScheduleRequest
{
    /// <summary>Wipe existing matches before generating; default true. Setting false lets the
    /// admin append matches instead of a full rebuild.</summary>
    public bool ReplaceExisting { get; init; } = true;
}

/// <summary>One calendar date the event runs, with optional daily start/end times.</summary>
public record HostedTournamentDayDto(
    int Id,
    DateOnly Date,
    TimeOnly? StartTime,
    TimeOnly? EndTime,
    string? Notes,
    DateTime CreatedAt);

public record SaveHostedTournamentTierRequest
{
    [Required, MaxLength(80)] public string Name { get; init; } = string.Empty;
    public int SortOrder { get; init; }
    [MaxLength(500)] public string? Notes { get; init; }
}

public record SaveHostedTournamentDayRequest
{
    [Required] public DateOnly Date { get; init; }
    public TimeOnly? StartTime { get; init; }
    public TimeOnly? EndTime { get; init; }
    [MaxLength(500)] public string? Notes { get; init; }
}

public record AssignTeamTierRequest
{
    /// <summary>Null clears the tier assignment.</summary>
    public int? TierId { get; init; }
}

public record SetTeamPaidRequest
{
    public bool Paid { get; init; }
    [MaxLength(120)] public string? PaymentMethod { get; init; }
    [MaxLength(120)] public string? PaymentReference { get; init; }
}

public record HostedTournamentDto(
    int Id,
    string Name,
    TournamentKind Kind,
    DateOnly StartDate,
    DateOnly? EndDate,
    int? VenueId,
    string? VenueName,
    string? VenueAddress,
    string? Location,
    decimal? CostPerTeam,
    string? Notes,
    string? RulesOfPlay,
    string? PublicSlug,
    int MatchDurationMinutes,
    int HalfMinutes,
    int HalftimeMinutes,
    int MinutesBetweenGames,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    IReadOnlyList<HostedTournamentTeamDto> Teams,
    IReadOnlyList<HostedTournamentTierDto> Tiers,
    IReadOnlyList<HostedTournamentDayDto> Days,
    IReadOnlyList<HostedTournamentFieldDto> Fields,
    IReadOnlyList<HostedTournamentMatchDto> Matches);

public record SaveHostedTournamentRequest
{
    [Required, MaxLength(160)] public string Name { get; init; } = string.Empty;
    public TournamentKind Kind { get; init; } = TournamentKind.Tournament;
    [Required] public DateOnly StartDate { get; init; }
    public DateOnly? EndDate { get; init; }
    public int? VenueId { get; init; }
    [MaxLength(400)] public string? Location { get; init; }
    [Range(0, 1_000_000)] public decimal? CostPerTeam { get; init; }
    [MaxLength(2000)] public string? Notes { get; init; }
    /// <summary>Free-form body used as the email content when the schedule is sent AND as the
    /// header text on the public schedule page.</summary>
    public string? RulesOfPlay { get; init; }
    [Range(10, 240)] public int MatchDurationMinutes { get; init; } = 60;
    /// <summary>Length of one half in minutes. Combined with HalftimeMinutes drives the match
    /// window used by the scheduler.</summary>
    [Range(5, 90)] public int HalfMinutes { get; init; } = 25;
    /// <summary>Break between halves.</summary>
    [Range(0, 60)] public int HalftimeMinutes { get; init; } = 5;
    /// <summary>Gap the scheduler leaves between back-to-back matches on the same field.</summary>
    [Range(0, 60)] public int MinutesBetweenGames { get; init; } = 5;
}

/// <summary>Admin edit of a single match — re-slot day / field / start time and swap the
/// participating teams. Null fields on incoming request are treated as "unset" (unschedule
/// the match) so the admin can drag a match back into the unscheduled pool.</summary>
public record SaveHostedTournamentMatchRequest
{
    public int? TierId { get; init; }
    public int? TeamAId { get; init; }
    public int? TeamBId { get; init; }
    public int? FieldId { get; init; }
    public int? DayId { get; init; }
    public TimeOnly? StartTime { get; init; }
    [Range(10, 240)] public int? DurationMinutes { get; init; }
    [MaxLength(500)] public string? Notes { get; init; }
}

/// <summary>Public-facing schedule payload — safe to return without auth. Includes the
/// event's headline info, rules body, day windows, fields, and every scheduled match.</summary>
public record PublicScheduleDto(
    string Name,
    TournamentKind Kind,
    DateOnly StartDate,
    DateOnly? EndDate,
    string? VenueName,
    string? VenueAddress,
    string? Location,
    string? RulesOfPlay,
    IReadOnlyList<HostedTournamentDayDto> Days,
    IReadOnlyList<HostedTournamentFieldDto> Fields,
    IReadOnlyList<HostedTournamentMatchDto> Matches);

public record AddHostedTournamentTeamRequest
{
    /// <summary>Set exactly one of LvssTeamId or InvitedTeamId. The controller rejects both / neither.</summary>
    public int? LvssTeamId { get; init; }
    public int? InvitedTeamId { get; init; }
    [MaxLength(500)] public string? Notes { get; init; }
}
