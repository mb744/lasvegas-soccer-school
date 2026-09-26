using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using SoccerSchool.Api.Data;
using SoccerSchool.Api.Domain;

namespace SoccerSchool.Api.Services;

/// <summary>What one adult login may do right now.</summary>
/// <param name="CoachTeamIds">Teams whose coach card is linked to the login — the scope for any
/// coach permission. Empty for non-coaches.</param>
/// <param name="Keys">Effective permission keys: every key for admins, otherwise the union of the
/// Parent role, the Coach role (if coaching any team) and individual grants.</param>
public record EffectivePermissions(
    string UserId,
    bool IsAdmin,
    IReadOnlyList<int> CoachTeamIds,
    IReadOnlyList<AccessRole> Roles,
    IReadOnlySet<string> Keys)
{
    public bool IsCoach => CoachTeamIds.Count > 0;
    public bool Has(string permission) => Keys.Contains(permission);
    public bool HasAny(IEnumerable<string> permissions) => permissions.Any(Keys.Contains);
}

public interface IPermissionService
{
    /// <summary>Effective permissions for the signed-in adult, or null for anyone else (anonymous,
    /// or a kid's Daily Training token, which never carries an Identity user).</summary>
    Task<EffectivePermissions?> GetAsync(ClaimsPrincipal principal, CancellationToken ct);

    Task<EffectivePermissions> GetForUserAsync(ApplicationUser user, CancellationToken ct);

    /// <summary>Enabled permission keys per editable role (Coach, Parent).</summary>
    Task<IReadOnlyDictionary<AccessRole, IReadOnlySet<string>>> GetRoleMatrixAsync(CancellationToken ct);

    /// <summary>Turns a permission on or off for the Coach or Parent role. Returns an error message
    /// or null.</summary>
    Task<string?> SetRolePermissionAsync(AccessRole role, string permission, bool enabled, ClaimsPrincipal actor, CancellationToken ct);

    Task<IReadOnlyList<string>> GetUserGrantsAsync(string userId, CancellationToken ct);

    /// <summary>Grants or revokes a grantable permission for one person. Returns an error message
    /// or null.</summary>
    Task<string?> SetUserGrantAsync(string userId, string permission, bool enabled, ClaimsPrincipal actor, CancellationToken ct);

    /// <summary>Records a change to the Identity Admin role (made by AdminUsersController).</summary>
    Task AuditAdminRoleChangeAsync(ApplicationUser target, bool granted, ClaimsPrincipal actor, CancellationToken ct);

    /// <summary>Applies role defaults for catalogue permissions seen for the first time. Idempotent;
    /// run at startup.</summary>
    Task EnsureSeededAsync(CancellationToken ct);
}

public class PermissionService : IPermissionService
{
    /// <summary>Short enough that a revoke takes effect almost immediately even on another server
    /// instance; changes made through this service invalidate the local cache at once.</summary>
    private static readonly TimeSpan CacheFor = TimeSpan.FromSeconds(30);
    private const string RoleMatrixKey = "rbac:role-matrix";

    // Bumped on every change so cached per-user results are ignored after an edit.
    private static long _generation;

    private readonly AppDbContext _db;
    private readonly UserManager<ApplicationUser> _users;
    private readonly ICoachScopeService _coaches;
    private readonly IMemoryCache _cache;
    private readonly ILogger<PermissionService> _logger;

    public PermissionService(
        AppDbContext db,
        UserManager<ApplicationUser> users,
        ICoachScopeService coaches,
        IMemoryCache cache,
        ILogger<PermissionService> logger)
    {
        _db = db;
        _users = users;
        _coaches = coaches;
        _cache = cache;
        _logger = logger;
    }

    public async Task<EffectivePermissions?> GetAsync(ClaimsPrincipal principal, CancellationToken ct)
    {
        var userId = _users.GetUserId(principal);
        if (string.IsNullOrEmpty(userId)) return null;

        var cacheKey = $"rbac:user:{userId}:{Interlocked.Read(ref _generation)}";
        if (_cache.TryGetValue(cacheKey, out EffectivePermissions? cached) && cached is not null) return cached;

        var user = await _users.FindByIdAsync(userId);
        if (user is null) return null;
        var result = await GetForUserAsync(user, ct);
        _cache.Set(cacheKey, result, CacheFor);
        return result;
    }

    public async Task<EffectivePermissions> GetForUserAsync(ApplicationUser user, CancellationToken ct)
    {
        var isAdmin = await _users.IsInRoleAsync(user, Roles.Admin);
        var coachTeamIds = await _coaches.GetCoachTeamIdsAsync(user, ct);

        var roles = new List<AccessRole>();
        if (isAdmin) roles.Add(AccessRole.Admin);
        if (coachTeamIds.Count > 0) roles.Add(AccessRole.Coach);
        roles.Add(AccessRole.Parent);

        if (isAdmin)
            return new EffectivePermissions(user.Id, true, coachTeamIds, roles, Permissions.Keys);

        var matrix = await GetRoleMatrixAsync(ct);
        var keys = new HashSet<string>(StringComparer.Ordinal);
        foreach (var role in roles)
            if (matrix.TryGetValue(role, out var rolePerms)) keys.UnionWith(rolePerms);
        foreach (var grant in await GetUserGrantsAsync(user.Id, ct))
            keys.Add(grant);
        keys.IntersectWith(Permissions.Keys); // ignore rows for permissions removed from the catalogue

        return new EffectivePermissions(user.Id, false, coachTeamIds, roles, keys);
    }

    public async Task<IReadOnlyDictionary<AccessRole, IReadOnlySet<string>>> GetRoleMatrixAsync(CancellationToken ct)
    {
        var key = $"{RoleMatrixKey}:{Interlocked.Read(ref _generation)}";
        if (_cache.TryGetValue(key, out IReadOnlyDictionary<AccessRole, IReadOnlySet<string>>? cached) && cached is not null)
            return cached;

        var rows = await _db.RolePermissions.AsNoTracking().ToListAsync(ct);
        var matrix = new Dictionary<AccessRole, IReadOnlySet<string>>
        {
            [AccessRole.Coach] = rows.Where(r => r.Role == AccessRole.Coach).Select(r => r.Permission).ToHashSet(StringComparer.Ordinal),
            [AccessRole.Parent] = rows.Where(r => r.Role == AccessRole.Parent).Select(r => r.Permission).ToHashSet(StringComparer.Ordinal),
        };
        _cache.Set(key, (IReadOnlyDictionary<AccessRole, IReadOnlySet<string>>)matrix, CacheFor);
        return matrix;
    }

    public async Task<string?> SetRolePermissionAsync(AccessRole role, string permission, bool enabled, ClaimsPrincipal actor, CancellationToken ct)
    {
        if (role == AccessRole.Admin) return "The Admin role always has every permission.";
        if (!Permissions.IsKnown(permission)) return $"Unknown permission '{permission}'.";

        var existing = await _db.RolePermissions.FirstOrDefaultAsync(r => r.Role == role && r.Permission == permission, ct);
        if (enabled == (existing is not null)) return null; // already in that state

        if (enabled) _db.RolePermissions.Add(new RolePermission { Role = role, Permission = permission });
        else _db.RolePermissions.Remove(existing!);

        await AddAuditAsync(enabled ? PermissionAuditAction.RoleGranted : PermissionAuditAction.RoleRevoked,
            role, target: null, permission, actor, ct);
        await _db.SaveChangesAsync(ct);
        Invalidate();
        return null;
    }

    public async Task<IReadOnlyList<string>> GetUserGrantsAsync(string userId, CancellationToken ct) =>
        await _db.UserPermissionGrants.AsNoTracking()
            .Where(g => g.UserId == userId)
            .OrderBy(g => g.Permission)
            .Select(g => g.Permission)
            .ToListAsync(ct);

    public async Task<string?> SetUserGrantAsync(string userId, string permission, bool enabled, ClaimsPrincipal actor, CancellationToken ct)
    {
        var info = Permissions.Find(permission);
        if (info is null) return $"Unknown permission '{permission}'.";
        if (!info.Grantable) return $"'{permission}' can't be granted to individuals; change the role instead.";

        var target = await _users.FindByIdAsync(userId);
        if (target is null) return "User not found.";

        var existing = await _db.UserPermissionGrants.FirstOrDefaultAsync(g => g.UserId == userId && g.Permission == permission, ct);
        if (enabled == (existing is not null)) return null;

        if (enabled)
            _db.UserPermissionGrants.Add(new UserPermissionGrant
            {
                UserId = userId,
                Permission = permission,
                GrantedByUserId = _users.GetUserId(actor),
            });
        else _db.UserPermissionGrants.Remove(existing!);

        await AddAuditAsync(enabled ? PermissionAuditAction.UserGranted : PermissionAuditAction.UserRevoked,
            role: null, target, permission, actor, ct);
        await _db.SaveChangesAsync(ct);
        Invalidate();
        return null;
    }

    public async Task AuditAdminRoleChangeAsync(ApplicationUser target, bool granted, ClaimsPrincipal actor, CancellationToken ct)
    {
        await AddAuditAsync(granted ? PermissionAuditAction.AdminRoleGranted : PermissionAuditAction.AdminRoleRevoked,
            AccessRole.Admin, target, permission: null, actor, ct);
        await _db.SaveChangesAsync(ct);
        Invalidate();
    }

    public async Task EnsureSeededAsync(CancellationToken ct)
    {
        var known = (await _db.KnownPermissions.Select(k => k.Permission).ToListAsync(ct)).ToHashSet(StringComparer.Ordinal);
        var added = 0;
        foreach (var info in Permissions.All.Where(p => !known.Contains(p.Key)))
        {
            foreach (var role in info.DefaultRoles.Where(r => r != AccessRole.Admin))
            {
                var exists = await _db.RolePermissions.AnyAsync(r => r.Role == role && r.Permission == info.Key, ct);
                if (!exists) _db.RolePermissions.Add(new RolePermission { Role = role, Permission = info.Key });
            }
            _db.KnownPermissions.Add(new KnownPermission { Permission = info.Key });
            added++;
        }
        if (added == 0) return;
        await _db.SaveChangesAsync(ct);
        Invalidate();
        _logger.LogInformation("Seeded role defaults for {Count} new permission(s).", added);
    }

    private static void Invalidate() => Interlocked.Increment(ref _generation);

    private async Task AddAuditAsync(PermissionAuditAction action, AccessRole? role, ApplicationUser? target,
        string? permission, ClaimsPrincipal actor, CancellationToken ct)
    {
        var actorId = _users.GetUserId(actor);
        var actorUser = actorId is null ? null : await _users.FindByIdAsync(actorId);
        _db.PermissionAuditEntries.Add(new PermissionAuditEntry
        {
            Action = action,
            Role = role,
            TargetUserId = target?.Id,
            TargetUserEmail = target?.Email,
            Permission = permission,
            ActorUserId = actorId,
            ActorEmail = actorUser?.Email,
        });
    }
}
