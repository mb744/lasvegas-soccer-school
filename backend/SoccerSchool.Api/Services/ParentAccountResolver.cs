using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using SoccerSchool.Api.Data;
using SoccerSchool.Api.Domain;

namespace SoccerSchool.Api.Services;

/// <summary>How a login relates to one family.</summary>
public enum FamilyRole
{
    Owner,
    Guardian,
    Viewer,
}

/// <summary>
/// Resolves the <see cref="ParentAccount"/> a signed-in user acts as. A family can have several
/// logins (the owner plus linked <see cref="ParentAccountCollaborator"/> accounts); guardians see and
/// do everything the owner can, view-only family members (<see cref="FamilyAccessLevel.Viewer"/>)
/// only see. Anything that changes something for a family or exposes private family data
/// (attendance, chat, invoices, registrations, kid logins) must use the Guardian variants.
/// </summary>
public interface IParentAccountResolver
{
    /// <summary>The family this user mainly belongs to, at any access level (see
    /// <see cref="ResolveByUserIdAsync"/> for the order). Null when they belong to none.</summary>
    Task<ParentAccount?> ResolveAsync(ClaimsPrincipal user, CancellationToken ct);

    /// <summary>Same as <see cref="ResolveAsync(ClaimsPrincipal, CancellationToken)"/> but from a raw
    /// user id — used by the SignalR hub, which has the id but not always a full principal.</summary>
    Task<ParentAccount?> ResolveByUserIdAsync(string userId, CancellationToken ct);

    /// <summary>The family this user can act for (owner or guardian); never a view-only one.</summary>
    Task<ParentAccount?> ResolveGuardianAsync(ClaimsPrincipal user, CancellationToken ct);

    Task<ParentAccount?> ResolveGuardianByUserIdAsync(string userId, CancellationToken ct);

    /// <summary>Every family this user belongs to: owned plus linked. With
    /// <paramref name="guardianOnly"/>, view-only links are left out.</summary>
    Task<List<int>> FamilyIdsAsync(string userId, bool guardianOnly, CancellationToken ct);

    /// <summary>The user's role on one family, or null when they aren't part of it.</summary>
    Task<FamilyRole?> RoleInAsync(string userId, int parentAccountId, CancellationToken ct);
}

public class ParentAccountResolver : IParentAccountResolver
{
    private readonly AppDbContext _db;
    private readonly UserManager<ApplicationUser> _users;

    public ParentAccountResolver(AppDbContext db, UserManager<ApplicationUser> users)
    {
        _db = db;
        _users = users;
    }

    public Task<ParentAccount?> ResolveAsync(ClaimsPrincipal user, CancellationToken ct) =>
        ByPrincipal(user, id => ResolveByUserIdAsync(id, ct));

    public Task<ParentAccount?> ResolveGuardianAsync(ClaimsPrincipal user, CancellationToken ct) =>
        ByPrincipal(user, id => ResolveGuardianByUserIdAsync(id, ct));

    public Task<ParentAccount?> ResolveByUserIdAsync(string userId, CancellationToken ct) =>
        ResolveCoreAsync(userId, includeViewer: true, ct);

    public Task<ParentAccount?> ResolveGuardianByUserIdAsync(string userId, CancellationToken ct) =>
        ResolveCoreAsync(userId, includeViewer: false, ct);

    /// <summary>Order: an owned family with kids; else the oldest family they're a guardian on; else
    /// (when allowed) the oldest view-only family; else their owned family even if it's empty. The
    /// "with kids" check keeps someone who signed up on their own (creating an empty family) and was
    /// then linked to a real family from landing in the empty one.</summary>
    private async Task<ParentAccount?> ResolveCoreAsync(string userId, bool includeViewer, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(userId)) return null;

        var owned = await _db.ParentAccounts.FirstOrDefaultAsync(a => a.UserId == userId, ct);
        if (owned is not null && await _db.Players.AnyAsync(p => p.ParentAccountId == owned.Id, ct))
            return owned;

        var links = await _db.ParentAccountCollaborators
            .Where(c => c.UserId == userId && (owned == null || c.ParentAccountId != owned.Id))
            .Where(c => includeViewer || c.AccessLevel == FamilyAccessLevel.Guardian)
            .OrderBy(c => c.AccessLevel).ThenBy(c => c.CreatedAt)
            .Select(c => c.ParentAccount)
            .FirstOrDefaultAsync(ct);
        return links ?? owned;
    }

    public async Task<List<int>> FamilyIdsAsync(string userId, bool guardianOnly, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(userId)) return new List<int>();
        var ids = await _db.ParentAccounts.Where(a => a.UserId == userId).Select(a => a.Id).ToListAsync(ct);
        var linked = await _db.ParentAccountCollaborators
            .Where(c => c.UserId == userId && (!guardianOnly || c.AccessLevel == FamilyAccessLevel.Guardian))
            .Select(c => c.ParentAccountId)
            .ToListAsync(ct);
        ids.AddRange(linked.Where(id => !ids.Contains(id)));
        return ids;
    }

    public async Task<FamilyRole?> RoleInAsync(string userId, int parentAccountId, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(userId)) return null;
        if (await _db.ParentAccounts.AnyAsync(a => a.Id == parentAccountId && a.UserId == userId, ct))
            return FamilyRole.Owner;
        var level = await _db.ParentAccountCollaborators
            .Where(c => c.UserId == userId && c.ParentAccountId == parentAccountId)
            .Select(c => (FamilyAccessLevel?)c.AccessLevel)
            .FirstOrDefaultAsync(ct);
        return level switch
        {
            FamilyAccessLevel.Guardian => FamilyRole.Guardian,
            FamilyAccessLevel.Viewer => FamilyRole.Viewer,
            _ => null,
        };
    }

    private Task<ParentAccount?> ByPrincipal(ClaimsPrincipal user, Func<string, Task<ParentAccount?>> resolve)
    {
        var userId = _users.GetUserId(user);
        return string.IsNullOrEmpty(userId) ? Task.FromResult<ParentAccount?>(null) : resolve(userId);
    }
}
