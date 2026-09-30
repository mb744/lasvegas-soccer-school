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

    /// <summary>True when the person turned off push notifications to their phones (Profile →
    /// Notifications, or the preferences link in event emails). Checked by the push sender for
    /// every push. Stored as "muted" so existing accounts default to getting pushes.</summary>
    public bool PushMuted { get; set; }

    /// <summary>Email me about each game (new, changed, reminder).</summary>
    public EmailPreference GameEmails { get; set; }

    /// <summary>Email me about each practice or other event (new, changed, reminder).</summary>
    public EmailPreference EventEmails { get; set; }
}
