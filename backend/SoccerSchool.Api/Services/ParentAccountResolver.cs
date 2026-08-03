using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using SoccerSchool.Api.Data;
using SoccerSchool.Api.Domain;

namespace SoccerSchool.Api.Services;

/// <summary>
/// Resolves the <see cref="ParentAccount"/> a signed-in user acts as. A family can have several
/// logins (the owner plus linked <see cref="ParentAccountCollaborator"/> mom/dad accounts); any of
/// them should see the same kids, schedule, attendance, and chats. The web's per-controller
/// owner-only lookup predates collaborators; the mobile surface uses this shared resolver so the
/// whole family is covered consistently.
/// </summary>
public interface IParentAccountResolver
{
    /// <summary>The account this user owns, or — failing that — the first family they collaborate on.
    /// Null when the user has no parent account at all (shouldn't happen for a real parent login).</summary>
    Task<ParentAccount?> ResolveAsync(ClaimsPrincipal user, CancellationToken ct);

    /// <summary>Same as <see cref="ResolveAsync(ClaimsPrincipal, CancellationToken)"/> but from a raw
    /// user id — used by the SignalR hub, which has the id but not always a full principal.</summary>
    Task<ParentAccount?> ResolveByUserIdAsync(string userId, CancellationToken ct);
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

    public Task<ParentAccount?> ResolveAsync(ClaimsPrincipal user, CancellationToken ct)
    {
        var userId = _users.GetUserId(user);
        return string.IsNullOrEmpty(userId)
            ? Task.FromResult<ParentAccount?>(null)
            : ResolveByUserIdAsync(userId, ct);
    }

    public async Task<ParentAccount?> ResolveByUserIdAsync(string userId, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(userId)) return null;

        var owned = await _db.ParentAccounts.FirstOrDefaultAsync(a => a.UserId == userId, ct);
        if (owned is not null) return owned;

        // Collaborator (e.g. the second parent linked to an existing family).
        var collab = await _db.ParentAccountCollaborators
            .Where(c => c.UserId == userId)
            .OrderBy(c => c.CreatedAt)
            .Select(c => c.ParentAccount)
            .FirstOrDefaultAsync(ct);
        return collab;
    }
}
