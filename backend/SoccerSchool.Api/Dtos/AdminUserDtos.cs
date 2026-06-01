namespace SoccerSchool.Api.Dtos;

public record UserSummary(
    string Id,
    string Email,
    string FirstName,
    string LastName,
    string? Phone,
    bool IsAdmin,
    bool IsBanned,
    DateTime? CreatedAt,
    DateTime? LastLoginAt,
    int RegistrationCount,
    /// <summary>The user's ParentAccount id, or null when the Identity user has no parent
    /// profile yet (e.g. seed admin accounts). Targets admin actions that need a parent.</summary>
    int? ParentAccountId
);
