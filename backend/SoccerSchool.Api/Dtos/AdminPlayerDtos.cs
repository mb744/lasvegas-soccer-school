using System.ComponentModel.DataAnnotations;

namespace SoccerSchool.Api.Dtos;

/// <summary>One player as the admin /admin/players page sees them — durable player fields plus
/// parent contact, current-season registration status, current team, and the count + summary of
/// any uniform assignments. Built from joins so admin can filter without round trips.</summary>
public record AdminPlayerSummaryDto(
    int Id,
    string FirstName,
    string LastName,
    DateOnly DateOfBirth,
    /// <summary>Age bracket name from the player's most recent registration's AgeClassification,
    /// or null if no registration on file / no classification covers the DOB.</summary>
    string? AgeBracket,
    int? ParentAccountId,
    string? ParentName,
    string? ParentCellPhone,
    string? ParentEmail,
    int? CurrentTeamId,
    string? CurrentTeamName,
    /// <summary>True when the player has a SignedAt waiver on the active-season registration row.</summary>
    bool WaiverSigned,
    /// <summary>True when an active-season RegistrationPlayer row exists at all (signed or not).</summary>
    bool RegisteredThisSeason,
    int UniformCount,
    /// <summary>Comma-joined jersey numbers from active (non-returned) uniform assignments,
    /// for an at-a-glance display in the admin list. Empty when no active assignments.</summary>
    string ActiveJerseyNumbers);

/// <summary>Full uniform-assignment row for the player detail panel.</summary>
public record PlayerUniformAssignmentDto(
    int Id,
    int UniformId,
    string UniformName,
    string? UniformDesignation,
    string JerseyNumber,
    DateOnly AssignedAt,
    DateOnly? ReturnedAt,
    string? Notes,
    DateTime CreatedAt);

public record CreatePlayerUniformAssignmentRequest
{
    [Required] public int UniformId { get; init; }
    [Required, MaxLength(16)] public string JerseyNumber { get; init; } = string.Empty;
    [Required] public DateOnly AssignedAt { get; init; }
    [MaxLength(500)] public string? Notes { get; init; }
}

public record UpdatePlayerUniformAssignmentRequest
{
    [Required, MaxLength(16)] public string JerseyNumber { get; init; } = string.Empty;
    [Required] public DateOnly AssignedAt { get; init; }
    public DateOnly? ReturnedAt { get; init; }
    [MaxLength(500)] public string? Notes { get; init; }
}

/// <summary>Admin creates a player and binds it to a parent account. Either picks an existing
/// parent by id or supplies enough info to spin up a new ParentAccount with a placeholder user
/// (no password — they redeem via the registration invite link).</summary>
public record AdminCreatePlayerRequest
{
    [Required, MaxLength(80)] public string FirstName { get; init; } = string.Empty;
    [Required, MaxLength(80)] public string LastName { get; init; } = string.Empty;
    [Required] public DateOnly DateOfBirth { get; init; }

    /// <summary>When set, attach the player to this existing ParentAccount. When null, the
    /// New* fields below must be populated and a fresh ParentAccount + ApplicationUser are
    /// created — the parent redeems via the registration invite email.</summary>
    public int? ParentAccountId { get; init; }

    [MaxLength(80)] public string? NewParentFirstName { get; init; }
    [MaxLength(80)] public string? NewParentLastName { get; init; }
    [MaxLength(256)] public string? NewParentEmail { get; init; }
    [MaxLength(32)] public string? NewParentCellPhone { get; init; }
}

/// <summary>Admin asks the system to email a fresh registration link to a parent. Used both
/// for newly-created parents and for nudging existing parents to complete the active-season
/// registration / waiver.</summary>
public record SendRegistrationInviteRequest
{
    [Required] public int ParentAccountId { get; init; }
    [MaxLength(1000)] public string? AdditionalNote { get; init; }
}

public record SendRegistrationInviteResult(bool Success, string Message);
