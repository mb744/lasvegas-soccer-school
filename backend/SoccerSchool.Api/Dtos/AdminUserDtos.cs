using System.ComponentModel.DataAnnotations;

namespace SoccerSchool.Api.Dtos;

public record UserSummary(
    string Id,
    string Email,
    string FirstName,
    string LastName,
    string? Phone,
    bool IsAdmin,
    /// <summary>True when this login's email appears on any TeamCoach card. Derived, not stored —
    /// same rule the mobile app and chat-group admin use.</summary>
    bool IsCoach,
    bool IsBanned,
    DateTime? CreatedAt,
    DateTime? LastLoginAt,
    int RegistrationCount,
    /// <summary>The user's ParentAccount id, or null when the Identity user has no parent
    /// profile yet (e.g. seed admin accounts). Targets admin actions that need a parent.</summary>
    int? ParentAccountId
);

/// <summary>Rename request — updates ParentAccount.FirstName/LastName. A user without a parent
/// account (rare — usually only seed admins) rejects with 400.</summary>
public record UpdateUserProfileRequest
{
    [Required, MaxLength(64)]
    public string FirstName { get; init; } = string.Empty;

    [Required, MaxLength(64)]
    public string LastName { get; init; } = string.Empty;
}

/// <summary>Idempotent add/remove of the site-wide Admin role. Coach status is not a role —
/// it's derived from TeamCoach cards; grant/revoke coach by editing the team roster.</summary>
public record SetUserAdminRequest
{
    public bool IsAdmin { get; init; }
}
