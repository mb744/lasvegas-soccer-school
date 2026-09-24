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

/// <summary>One team this user coaches — a TeamCoach card where the card's Email matches the
/// user's login. Includes the card's row id so callers can distinguish "this user's coach card"
/// from other coach cards on the same team.</summary>
public record UserCoachTeamDto(int TeamCoachId, int TeamId, string TeamName);

/// <summary>Full-state replacement of this user's coach-team assignments. Server creates a
/// TeamCoach card for each new team (name + phone + language pulled from the user's profile)
/// and deletes any existing coach cards on teams removed from the list. Safe to send the same
/// list twice — it's a no-op.</summary>
public record SetUserCoachTeamsRequest
{
    public IReadOnlyList<int> TeamIds { get; init; } = Array.Empty<int>();
}
