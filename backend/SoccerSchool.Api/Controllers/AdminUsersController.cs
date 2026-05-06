using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SoccerSchool.Api.Data;
using SoccerSchool.Api.Domain;
using SoccerSchool.Api.Dtos;

namespace SoccerSchool.Api.Controllers;

[ApiController]
[Route("api/admin/users")]
[Authorize(Roles = Roles.Admin)]
public class AdminUsersController : ControllerBase
{
    private readonly UserManager<ApplicationUser> _users;
    private readonly AppDbContext _db;

    public AdminUsersController(UserManager<ApplicationUser> users, AppDbContext db)
    {
        _users = users;
        _db = db;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<UserSummary>>> List(CancellationToken ct)
    {
        // Pull users + their parent account + role membership in one round-trip.
        var users = await _db.Users
            .Select(u => new
            {
                u.Id,
                u.Email,
                u.LockoutEnd,
                u.LastLoginAt,
                Account = _db.ParentAccounts.Where(p => p.UserId == u.Id)
                    .Select(p => new { p.Id, p.FirstName, p.LastName, p.CellPhone, p.CreatedAt })
                    .FirstOrDefault(),
                IsAdmin = (
                    from ur in _db.UserRoles
                    join r in _db.Roles on ur.RoleId equals r.Id
                    where ur.UserId == u.Id && r.Name == Roles.Admin
                    select r.Id
                ).Any(),
                RegistrationCount = _db.Registrations.Count(r => r.ParentAccount!.UserId == u.Id),
            })
            .OrderByDescending(u => u.Account != null ? u.Account.CreatedAt : (DateTime?)null)
            .Take(500)
            .ToListAsync(ct);

        var now = DateTimeOffset.UtcNow;
        return Ok(users.Select(u => new UserSummary(
            u.Id,
            u.Email ?? "",
            u.Account?.FirstName ?? "",
            u.Account?.LastName ?? "",
            u.Account?.CellPhone,
            u.IsAdmin,
            u.LockoutEnd is { } end && end > now,
            u.Account?.CreatedAt,
            u.LastLoginAt,
            u.RegistrationCount
        )).ToList());
    }

    [HttpPost("{id}/ban")]
    public async Task<IActionResult> Ban(string id, CancellationToken ct)
    {
        var (user, denied) = await ResolveTargetAsync(id);
        if (denied is not null) return denied;
        await _users.SetLockoutEnabledAsync(user!, true);
        await _users.SetLockoutEndDateAsync(user!, DateTimeOffset.MaxValue);
        return NoContent();
    }

    [HttpPost("{id}/unban")]
    public async Task<IActionResult> Unban(string id, CancellationToken ct)
    {
        var user = await _users.FindByIdAsync(id);
        if (user is null) return NotFound();
        await _users.SetLockoutEndDateAsync(user, null);
        return NoContent();
    }

    private async Task<(ApplicationUser? user, IActionResult? denied)> ResolveTargetAsync(string id)
    {
        var user = await _users.FindByIdAsync(id);
        if (user is null) return (null, NotFound());

        var currentUserId = _users.GetUserId(User);
        if (user.Id == currentUserId)
            return (null, BadRequest("Cannot ban your own account."));

        if (await _users.IsInRoleAsync(user, Roles.Admin))
            return (null, BadRequest("Cannot ban another admin."));

        return (user, null);
    }
}
