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
    private readonly Services.IPermissionService _permissions;

    public AdminUsersController(UserManager<ApplicationUser> users, AppDbContext db, Services.IPermissionService permissions)
    {
        _users = users;
        _db = db;
        _permissions = permissions;
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
                u.NormalizedEmail,
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

        // Coach linkage: cards with an explicit TeamCoach.UserId are the definitive answer.
        // Any coach card that was created before the coach signed in still has UserId = null and
        // only carries an email string — those get lazily reconciled here so the tag shows up on
        // the very first list refresh instead of waiting for the coach's next login.
        await BackfillCoachUserLinksAsync(ct);
        var coachUserIds = await _db.TeamCoaches
            .Where(tc => tc.UserId != null)
            .Select(tc => tc.UserId!)
            .Distinct()
            .ToListAsync(ct);
        var coachUserIdSet = coachUserIds.ToHashSet(StringComparer.Ordinal);

        var now = DateTimeOffset.UtcNow;
        return Ok(users.Select(u => new UserSummary(
            u.Id,
            u.Email ?? "",
            u.Account?.FirstName ?? "",
            u.Account?.LastName ?? "",
            u.Account?.CellPhone,
            u.IsAdmin,
            coachUserIdSet.Contains(u.Id),
            u.LockoutEnd is { } end && end > now,
            u.Account?.CreatedAt,
            u.LastLoginAt,
            u.RegistrationCount,
            u.Account?.Id
        )).ToList());
    }

    /// <summary>Reconciles orphaned TeamCoach.UserId nulls with any ApplicationUser whose
    /// NormalizedEmail matches the card's Email. Runs on every admin-list load — bounded by
    /// the number of *orphaned* cards (typically zero after the first pass), and matches by a
    /// single indexed hashset lookup. Same rule the mobile sign-in uses; running it in both spots
    /// means whichever happens first (admin views the list, coach signs in) resolves the link.
    /// Only logins with a verified email qualify — otherwise signing up with a coach's address
    /// would claim their card. Admins can still link an unverified user explicitly (SetCoachTeams).</summary>
    private async Task BackfillCoachUserLinksAsync(CancellationToken ct)
    {
        var orphaned = await _db.TeamCoaches
            .Where(tc => tc.UserId == null && tc.Email != null && tc.Email != "")
            .ToListAsync(ct);
        if (orphaned.Count == 0) return;

        var normalizedEmailsForUsers = orphaned
            .Select(tc => tc.Email!.Trim().ToUpperInvariant())
            .Distinct()
            .ToList();
        var userByNormalizedEmail = (await _db.Users
                .Where(u => u.EmailConfirmed && u.NormalizedEmail != null && normalizedEmailsForUsers.Contains(u.NormalizedEmail))
                .Select(u => new { u.Id, u.NormalizedEmail })
                .ToListAsync(ct))
            .ToDictionary(x => x.NormalizedEmail!, x => x.Id, StringComparer.Ordinal);

        var changed = false;
        foreach (var tc in orphaned)
        {
            var key = tc.Email!.Trim().ToUpperInvariant();
            if (userByNormalizedEmail.TryGetValue(key, out var uid))
            {
                tc.UserId = uid;
                changed = true;
            }
        }
        if (changed) await _db.SaveChangesAsync(ct);
    }

    /// <summary>Rename a user (updates their ParentAccount, which is what every other screen
    /// reads). Admin-only. Fails when the target has no parent account yet — usually only the
    /// seed admin login, which is edited directly in the DB.</summary>
    [HttpPut("{id}/profile")]
    public async Task<IActionResult> UpdateProfile(string id, [FromBody] UpdateUserProfileRequest req, CancellationToken ct)
    {
        var user = await _users.FindByIdAsync(id);
        if (user is null) return NotFound();

        var account = await _db.ParentAccounts.FirstOrDefaultAsync(p => p.UserId == id, ct);
        if (account is null) return BadRequest("User has no parent profile to rename.");

        var first = req.FirstName?.Trim();
        var last = req.LastName?.Trim();
        if (string.IsNullOrWhiteSpace(first) || string.IsNullOrWhiteSpace(last))
            return BadRequest("First and last name are required.");

        account.FirstName = first!;
        account.LastName = last!;
        await _db.SaveChangesAsync(ct);
        return NoContent();
    }

    /// <summary>Grant or revoke the site-wide Admin role. Guards against self-demotion — an admin
    /// can't remove their own admin bit (prevents "oops, now nobody can admin"). Idempotent: setting
    /// to the state the user is already in is a 204 no-op.</summary>
    [HttpPut("{id}/role")]
    public async Task<IActionResult> SetAdmin(string id, [FromBody] SetUserAdminRequest req, CancellationToken ct)
    {
        var user = await _users.FindByIdAsync(id);
        if (user is null) return NotFound();

        var currentUserId = _users.GetUserId(User);
        var isSelf = string.Equals(user.Id, currentUserId, StringComparison.Ordinal);
        var isCurrentlyAdmin = await _users.IsInRoleAsync(user, Roles.Admin);

        if (req.IsAdmin == isCurrentlyAdmin) return NoContent();
        if (isSelf && !req.IsAdmin) return BadRequest("Cannot revoke your own admin role.");

        var result = req.IsAdmin
            ? await _users.AddToRoleAsync(user, Roles.Admin)
            : await _users.RemoveFromRoleAsync(user, Roles.Admin);
        if (!result.Succeeded) return BadRequest(string.Join("; ", result.Errors.Select(e => e.Description)));
        await _permissions.AuditAdminRoleChangeAsync(user, req.IsAdmin, User, ct);
        return NoContent();
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

    /// <summary>Teams this user currently coaches — i.e. TeamCoach cards whose Email matches the
    /// user's login. Returned even when empty so the UI can render an "Add team" button.</summary>
    [HttpGet("{id}/coach-teams")]
    public async Task<ActionResult<IEnumerable<UserCoachTeamDto>>> ListCoachTeams(string id, CancellationToken ct)
    {
        var user = await _users.FindByIdAsync(id);
        if (user is null) return NotFound();
        if (string.IsNullOrEmpty(user.NormalizedEmail)) return Ok(Array.Empty<UserCoachTeamDto>());

        var rows = await _db.TeamCoaches
            .Where(tc => tc.Email != null && tc.Email.Trim().ToUpper() == user.NormalizedEmail)
            .OrderBy(tc => tc.Team!.Name)
            .Select(tc => new UserCoachTeamDto(tc.Id, tc.TeamId, tc.Team!.Name))
            .ToListAsync(ct);
        return Ok(rows);
    }

    /// <summary>Full-state replacement of this user's coach-team set. Adds a TeamCoach card
    /// (Name/Phone/Language taken from the parent profile so messaging routing works out of the
    /// box) for every team in the list they don't already coach; deletes their coach cards on
    /// teams removed from the list. Only touches cards whose Email matches this user — other
    /// coaches on the same team are left alone. Idempotent.</summary>
    [HttpPut("{id}/coach-teams")]
    public async Task<IActionResult> SetCoachTeams(string id, [FromBody] SetUserCoachTeamsRequest req, CancellationToken ct)
    {
        var user = await _users.FindByIdAsync(id);
        if (user is null) return NotFound();
        var email = user.Email;
        if (string.IsNullOrWhiteSpace(email))
            return BadRequest("User has no email — cannot assign coach role.");

        var desired = (req.TeamIds ?? Array.Empty<int>()).Distinct().ToHashSet();

        if (desired.Count > 0)
        {
            var known = await _db.Teams.Where(t => desired.Contains(t.Id)).Select(t => t.Id).ToListAsync(ct);
            var missing = desired.Except(known).ToList();
            if (missing.Count > 0) return BadRequest($"Unknown team ids: {string.Join(", ", missing)}");
        }

        var normalized = email.Trim().ToUpperInvariant();
        var existing = await _db.TeamCoaches
            .Where(tc => tc.Email != null && tc.Email.Trim().ToUpper() == normalized)
            .ToListAsync(ct);
        var existingByTeam = existing.ToDictionary(tc => tc.TeamId);

        foreach (var tc in existing.Where(x => !desired.Contains(x.TeamId)))
            _db.TeamCoaches.Remove(tc);

        var account = await _db.ParentAccounts.FirstOrDefaultAsync(p => p.UserId == user.Id, ct);
        var name = account is null
            ? user.Email ?? "Coach"
            : $"{account.FirstName} {account.LastName}".Trim();
        var phone = account?.CellPhone;
        var language = account?.Language ?? Domain.Language.English;

        foreach (var teamId in desired.Except(existingByTeam.Keys))
        {
            _db.TeamCoaches.Add(new Domain.TeamCoach
            {
                TeamId = teamId,
                Name = string.IsNullOrWhiteSpace(name) ? email : name,
                Email = email,
                Phone = phone,
                Language = language,
                Role = Domain.TeamCoachRole.HeadCoach,
                // The user already exists (we resolved them at the top of the method), so wire the
                // FK immediately instead of waiting for the coach's next sign-in to reconcile.
                UserId = user.Id,
            });
        }

        await _db.SaveChangesAsync(ct);
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
