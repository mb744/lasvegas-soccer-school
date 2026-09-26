using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SoccerSchool.Api.Data;
using SoccerSchool.Api.Domain;
using SoccerSchool.Api.Dtos;
using SoccerSchool.Api.Services;

namespace SoccerSchool.Api.Controllers;

/// <summary>
/// Admin management of role-based access: the permission catalogue, what the Coach and Parent roles
/// can do, and individual grants such as "Drill creator" or "Event creator". The Admin role always
/// has every permission and can't be edited here; admin membership itself is changed on the Users
/// page. Every change is written to the audit log.
/// </summary>
[ApiController]
[Route("api/admin/access")]
[Authorize(AuthenticationSchemes = AuthSchemes.CookieOrMobileJwt)]
public class AdminAccessController : ControllerBase
{
    private readonly IPermissionService _permissions;
    private readonly UserManager<ApplicationUser> _users;
    private readonly AppDbContext _db;

    public AdminAccessController(IPermissionService permissions, UserManager<ApplicationUser> users, AppDbContext db)
    {
        _permissions = permissions;
        _users = users;
        _db = db;
    }

    [HttpGet("catalog")]
    [RequirePermission(Permissions.RolesManage, Permissions.UsersManage)]
    public async Task<ActionResult<AccessCatalogDto>> Catalog(CancellationToken ct)
    {
        var matrix = await _permissions.GetRoleMatrixAsync(ct);
        return Ok(new AccessCatalogDto(
            Permissions.All.Select(p => new PermissionInfoDto(p.Key, p.Area, p.NameEn, p.NameEs, p.Grantable)).ToList(),
            Ordered(matrix[AccessRole.Coach]),
            Ordered(matrix[AccessRole.Parent])));
    }

    /// <summary>Turns a permission on/off for the Coach or Parent role.</summary>
    [HttpPut("roles/{role}/permissions/{permission}")]
    [RequirePermission(Permissions.RolesManage)]
    public async Task<IActionResult> SetRolePermission(string role, string permission, [FromBody] SetEnabledRequest req, CancellationToken ct)
    {
        if (!Enum.TryParse<AccessRole>(role, ignoreCase: true, out var parsed) || !Enum.IsDefined(parsed))
            return BadRequest("Role must be coach or parent.");
        var error = await _permissions.SetRolePermissionAsync(parsed, permission, req.Enabled, User, ct);
        return error is null ? NoContent() : BadRequest(error);
    }

    [HttpGet("users/{userId}")]
    [RequirePermission(Permissions.UsersManage)]
    public async Task<ActionResult<UserAccessDto>> GetUser(string userId, CancellationToken ct)
    {
        var user = await _users.FindByIdAsync(userId);
        if (user is null) return NotFound();
        var effective = await _permissions.GetForUserAsync(user, ct);
        var grants = await _permissions.GetUserGrantsAsync(userId, ct);
        return Ok(new UserAccessDto(
            user.Id,
            user.Email ?? "",
            effective.IsAdmin,
            effective.IsCoach,
            effective.CoachTeamIds,
            effective.Roles.Select(r => r.ToString().ToLowerInvariant()).ToList(),
            grants,
            Ordered(effective.Keys)));
    }

    /// <summary>Everyone with at least one individual grant (e.g. every Drill creator).</summary>
    [HttpGet("grants")]
    [RequirePermission(Permissions.UsersManage)]
    public async Task<ActionResult<IEnumerable<UserGrantsDto>>> ListGrants(CancellationToken ct)
    {
        var rows = await _db.UserPermissionGrants.AsNoTracking()
            .Select(g => new
            {
                g.UserId,
                g.Permission,
                Email = _db.Users.Where(u => u.Id == g.UserId).Select(u => u.Email).FirstOrDefault(),
                Name = _db.ParentAccounts.Where(p => p.UserId == g.UserId)
                    .Select(p => (p.FirstName + " " + p.LastName).Trim())
                    .FirstOrDefault(),
            })
            .ToListAsync(ct);
        var order = Permissions.All.Select((p, i) => (p.Key, i)).ToDictionary(x => x.Key, x => x.i);
        return Ok(rows
            .GroupBy(r => r.UserId)
            .Select(g => new UserGrantsDto(
                g.Key,
                g.First().Email ?? "",
                string.IsNullOrWhiteSpace(g.First().Name) ? g.First().Email ?? "" : g.First().Name!,
                g.Select(r => r.Permission)
                    .Where(order.ContainsKey)
                    .OrderBy(k => order[k])
                    .ToList()))
            .Where(u => u.Grants.Count > 0)
            .OrderBy(u => u.Name, StringComparer.OrdinalIgnoreCase)
            .ToList());
    }

    /// <summary>Grants/revokes a grantable permission (e.g. drills.create) for one person.</summary>
    [HttpPut("users/{userId}/grants/{permission}")]
    [RequirePermission(Permissions.UsersManage)]
    public async Task<IActionResult> SetUserGrant(string userId, string permission, [FromBody] SetEnabledRequest req, CancellationToken ct)
    {
        var error = await _permissions.SetUserGrantAsync(userId, permission, req.Enabled, User, ct);
        if (error == "User not found.") return NotFound();
        return error is null ? NoContent() : BadRequest(error);
    }

    [HttpGet("audit")]
    [RequirePermission(Permissions.RolesManage, Permissions.UsersManage)]
    public async Task<ActionResult<IEnumerable<PermissionAuditEntryDto>>> Audit([FromQuery] int take = 100, CancellationToken ct = default)
    {
        take = Math.Clamp(take, 1, 500);
        var rows = await _db.PermissionAuditEntries.AsNoTracking()
            .OrderByDescending(e => e.At).ThenByDescending(e => e.Id)
            .Take(take)
            .ToListAsync(ct);
        return Ok(rows.Select(e => new PermissionAuditEntryDto(
            e.Id, e.Action.ToString(), e.Role?.ToString().ToLowerInvariant(), e.TargetUserId, e.TargetUserEmail,
            e.Permission, e.ActorEmail, e.At)));
    }

    /// <summary>Keys in catalogue order, for stable UI rendering.</summary>
    private static List<string> Ordered(IReadOnlySet<string> keys) =>
        Permissions.All.Select(p => p.Key).Where(keys.Contains).ToList();
}
