using System.ComponentModel.DataAnnotations;

namespace SoccerSchool.Api.Domain;

/// <summary>A permission a non-admin role has by default (see <see cref="Permissions"/>). Rows exist
/// only for enabled pairs; Admin has every permission and is never stored.</summary>
public class RolePermission
{
    public int Id { get; set; }

    public AccessRole Role { get; set; }

    [Required, MaxLength(64)]
    public string Permission { get; set; } = string.Empty;
}

/// <summary>A permission granted to one person on top of their roles (e.g. "Drill creator" for one
/// coach). Additive only — there are no denies.</summary>
public class UserPermissionGrant
{
    public int Id { get; set; }

    [Required, MaxLength(450)]
    public string UserId { get; set; } = string.Empty;
    public ApplicationUser? User { get; set; }

    [Required, MaxLength(64)]
    public string Permission { get; set; } = string.Empty;

    [MaxLength(450)]
    public string? GrantedByUserId { get; set; }

    public DateTime GrantedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>Records which catalogue permissions have already had their role defaults applied, so a
/// permission added in a later release gets its defaults once — and a default an admin later turns
/// off stays off across restarts.</summary>
public class KnownPermission
{
    [Key, MaxLength(64)]
    public string Permission { get; set; } = string.Empty;

    public DateTime SeededAt { get; set; } = DateTime.UtcNow;
}

public enum PermissionAuditAction
{
    RoleGranted = 0,
    RoleRevoked = 1,
    UserGranted = 2,
    UserRevoked = 3,
    AdminRoleGranted = 4,
    AdminRoleRevoked = 5,
}

/// <summary>Who changed which permission, for whom, and when. Append-only.</summary>
public class PermissionAuditEntry
{
    public int Id { get; set; }

    public PermissionAuditAction Action { get; set; }

    /// <summary>Set for role changes.</summary>
    public AccessRole? Role { get; set; }

    /// <summary>Set for per-person changes. No FK: the log outlives the account.</summary>
    [MaxLength(450)]
    public string? TargetUserId { get; set; }

    [MaxLength(256)]
    public string? TargetUserEmail { get; set; }

    [MaxLength(64)]
    public string? Permission { get; set; }

    [MaxLength(450)]
    public string? ActorUserId { get; set; }

    [MaxLength(256)]
    public string? ActorEmail { get; set; }

    public DateTime At { get; set; } = DateTime.UtcNow;
}
