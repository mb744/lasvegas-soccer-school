using System.ComponentModel.DataAnnotations;

namespace SoccerSchool.Api.Domain;

/// <summary>
/// An additional parent/guardian contact on a <see cref="ParentAccount"/> (e.g. a second parent).
/// A reachable contact record only — it has no login of its own. Included in messaging recipient
/// resolution alongside the primary account holder so both guardians get broadcasts and team sends.
/// </summary>
public class ParentContact
{
    public int Id { get; set; }

    public int ParentAccountId { get; set; }
    public ParentAccount? ParentAccount { get; set; }

    [Required, MaxLength(80)]
    public string FirstName { get; set; } = string.Empty;

    [Required, MaxLength(80)]
    public string LastName { get; set; } = string.Empty;

    [MaxLength(256)]
    public string? Email { get; set; }

    /// <summary>E.164-normalized on write (via PhoneNormalizer), matching the primary parent's cell.</summary>
    [MaxLength(32)]
    public string? CellPhone { get; set; }

    public bool HasWhatsApp { get; set; }

    public Language Language { get; set; } = Language.English;

    /// <summary>Optional link to the Identity user this contact resolves to. Auto-populated when
    /// the additional parent first signs in with a matching email — the same auto-reconciliation
    /// pattern <see cref="TeamCoach.UserId"/> uses. Once linked, sign-in also spawns a
    /// <see cref="ParentAccountCollaborator"/> row so the new login sees the family's kids /
    /// schedule / chat automatically. Kept nullable so contacts can pre-exist before the person
    /// ever creates an account; SetNull on user delete so the contact row survives account
    /// resets.</summary>
    [MaxLength(450)]
    public string? UserId { get; set; }
    public ApplicationUser? User { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
