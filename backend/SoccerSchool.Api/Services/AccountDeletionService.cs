using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using SoccerSchool.Api.Data;
using SoccerSchool.Api.Domain;

namespace SoccerSchool.Api.Services;

/// <summary>
/// Runs the hard-purge that turns a mobile-app account deletion into a real anonymization.
/// Invoked by <see cref="AccountPurgeJob"/> once the user's <see cref="ApplicationUser.PendingDeletionAt"/>
/// has elapsed. See the accompanying controller for the two-phase workflow: the user first
/// schedules the deletion (soft state), and only after the grace window does this service execute.
/// </summary>
public interface IAccountDeletionService
{
    /// <summary>
    /// Full teardown for the user id: revoke every refresh token, delete push devices, drop chat
    /// memberships, anonymize the sender label on any messages they authored, delete any block or
    /// report rows referencing them, anonymize <see cref="ParentAccount"/> PII (if they own a
    /// family) and finally scrub the login itself (email/username/phone) and permanently lock it.
    /// Player rosters, registrations, and invoices are retained as legitimate school records.
    /// Idempotent — running twice on the same user is a no-op.
    /// </summary>
    Task PurgeAsync(string userId, CancellationToken ct);
}

public class AccountDeletionService : IAccountDeletionService
{
    private readonly AppDbContext _db;
    private readonly UserManager<ApplicationUser> _users;
    private readonly IReclaimHasher _reclaim;
    private readonly ILogger<AccountDeletionService> _logger;

    public AccountDeletionService(
        AppDbContext db,
        UserManager<ApplicationUser> users,
        IReclaimHasher reclaim,
        ILogger<AccountDeletionService> logger)
    {
        _db = db;
        _users = users;
        _reclaim = reclaim;
        _logger = logger;
    }

    public async Task PurgeAsync(string userId, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(userId)) return;
        var user = await _users.FindByIdAsync(userId);
        if (user is null) return;

        // A previous purge run may have already anonymized the login — detect and skip.
        if (user.Email is not null && user.Email.StartsWith("deleted-", StringComparison.OrdinalIgnoreCase)
            && user.Email.EndsWith("@removed.lvss.local", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        // 1. Revoke every active refresh token.
        var revokedAt = DateTime.UtcNow;
        var refreshes = await _db.MobileRefreshTokens.Where(r => r.UserId == userId && r.RevokedAt == null).ToListAsync(ct);
        foreach (var r in refreshes) r.RevokedAt = revokedAt;

        // 2. Delete push devices.
        var devices = await _db.DeviceTokens.Where(d => d.UserId == userId).ToListAsync(ct);
        _db.DeviceTokens.RemoveRange(devices);
        var installs = await _db.MobileAppInstalls.Where(i => i.UserId == userId).ToListAsync(ct);
        _db.MobileAppInstalls.RemoveRange(installs);

        // Individual permission grants (e.g. Drill creator) end with the account.
        var grants = await _db.UserPermissionGrants.Where(g => g.UserId == userId).ToListAsync(ct);
        _db.UserPermissionGrants.RemoveRange(grants);

        // 3. Drop chat memberships; anonymize any messages this user authored.
        var account = await _db.ParentAccounts.FirstOrDefaultAsync(p => p.UserId == userId, ct);
        var memberships = await _db.ChatGroupMembers
            .Where(m => m.UserId == userId || (account != null && m.ParentAccountId == account.Id))
            .ToListAsync(ct);
        _db.ChatGroupMembers.RemoveRange(memberships);

        var authored = await _db.ChatMessages.Where(m => m.SenderUserId == userId).ToListAsync(ct);
        foreach (var m in authored) m.SenderName = "Deleted user";

        // 4. Delete block/report rows.
        var blocks = await _db.ChatUserBlocks
            .Where(b => b.BlockerUserId == userId || b.BlockedUserId == userId)
            .ToListAsync(ct);
        _db.ChatUserBlocks.RemoveRange(blocks);

        // 5. Owner-family anonymization; collaborators just lose their link.
        if (account is not null)
        {
            var collaborators = await _db.ParentAccountCollaborators
                .Where(c => c.ParentAccountId == account.Id)
                .ToListAsync(ct);
            _db.ParentAccountCollaborators.RemoveRange(collaborators);

            // Stamp the reclaim hash BEFORE scrubbing — the plaintext email is only available
            // for one more moment. A future signup with the same address will be auto-linked
            // back to this family (see AuthController.Signup).
            account.ReclaimEmailHash = _reclaim.Hash(user.Email);
            account.FirstName = "Deleted";
            account.LastName = "User";
            account.CellPhone = null;
            account.AddressLine1 = null;
            account.AddressLine2 = null;
            account.City = null;
            account.PostalCode = null;
            account.NoCommunications = true;

            // Kids' Daily Training logins are credentials, not school records — remove them
            // (cascades to their sessions and reset links). Drill history stays on the players.
            var kidLogins = await _db.PlayerLogins
                .Where(l => l.Player!.ParentAccountId == account.Id)
                .ToListAsync(ct);
            _db.PlayerLogins.RemoveRange(kidLogins);
        }
        else
        {
            var collabLinks = await _db.ParentAccountCollaborators.Where(c => c.UserId == userId).ToListAsync(ct);
            _db.ParentAccountCollaborators.RemoveRange(collabLinks);
        }

        await _db.SaveChangesAsync(ct);

        // 6. Scrub the login itself and permanently lock. Clears PendingDeletionAt so a repeat
        //    purge is a no-op.
        var sentinel = $"deleted-{userId}@removed.lvss.local";
        user.Email = sentinel;
        user.NormalizedEmail = _users.NormalizeEmail(sentinel);
        user.UserName = sentinel;
        user.NormalizedUserName = _users.NormalizeName(sentinel);
        user.PhoneNumber = null;
        user.PhoneNumberConfirmed = false;
        user.EmailConfirmed = false;
        user.LockoutEnabled = true;
        user.LockoutEnd = DateTimeOffset.MaxValue;
        user.PendingDeletionAt = null;
        await _users.UpdateAsync(user);

        _logger.LogInformation("Purged account {UserId} after grace period.", userId);
    }
}
