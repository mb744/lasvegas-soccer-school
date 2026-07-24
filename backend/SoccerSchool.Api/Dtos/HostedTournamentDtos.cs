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
    DateTime CreatedAt);

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
    DateTime CreatedAt,
    DateTime UpdatedAt,
    IReadOnlyList<HostedTournamentTeamDto> Teams,
    IReadOnlyList<HostedTournamentTierDto> Tiers,
    IReadOnlyList<HostedTournamentDayDto> Days);

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
}

public record AddHostedTournamentTeamRequest
{
    /// <summary>Set exactly one of LvssTeamId or InvitedTeamId. The controller rejects both / neither.</summary>
    public int? LvssTeamId { get; init; }
    public int? InvitedTeamId { get; init; }
    [MaxLength(500)] public string? Notes { get; init; }
}
