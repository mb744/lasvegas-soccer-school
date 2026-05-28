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

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
