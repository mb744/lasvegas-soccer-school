using Microsoft.AspNetCore.Identity;

namespace SoccerSchool.Api.Domain;

public class ApplicationUser : IdentityUser
{
    public ParentAccount? ParentAccount { get; set; }

    /// <summary>UTC of the most recent successful sign-in (any path).</summary>
    public DateTime? LastLoginAt { get; set; }

    /// <summary>
    /// UTC the account is scheduled to be fully purged (chat memberships dropped, PII scrubbed,
    /// login permanently locked). Set when the user taps Delete account in the mobile app.
    /// <see cref="Services.IAccountDeletionService.PurgeAsync"/> runs from a background job when
    /// this time passes; until then the user can sign back in and cancel via
    /// <c>POST /api/mobile/auth/cancel-deletion</c>. Null = no pending deletion.
    /// </summary>
    public DateTime? PendingDeletionAt { get; set; }
}
