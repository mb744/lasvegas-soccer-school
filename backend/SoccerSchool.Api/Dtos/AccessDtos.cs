namespace SoccerSchool.Api.Dtos;

// ---- Role-based access control (api/admin/access) ----

public record PermissionInfoDto(string Key, string Area, string NameEn, string NameEs, bool Grantable);

/// <summary>The permission catalogue plus which keys each editable role has. Admin has every key.</summary>
public record AccessCatalogDto(
    IReadOnlyList<PermissionInfoDto> Permissions,
    IReadOnlyList<string> Coach,
    IReadOnlyList<string> Parent);

public record SetEnabledRequest
{
    public bool Enabled { get; init; }
}

/// <summary>One person's access: their roles, individual grants, and the resulting permissions.</summary>
public record UserAccessDto(
    string UserId,
    string Email,
    bool IsAdmin,
    bool IsCoach,
    IReadOnlyList<int> CoachTeamIds,
    IReadOnlyList<string> Roles,
    IReadOnlyList<string> Grants,
    IReadOnlyList<string> Effective);

public record PermissionAuditEntryDto(
    int Id,
    string Action,
    string? Role,
    string? TargetUserId,
    string? TargetUserEmail,
    string? Permission,
    string? ActorEmail,
    DateTime At);
