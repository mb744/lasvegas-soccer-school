using System.ComponentModel.DataAnnotations;

namespace SoccerSchool.Api.Domain;

/// <summary>
/// One uniform handed out to a player — pairs a player with a <see cref="Uniform"/> from the
/// club-wide catalog (Home/Away/Practice kit) plus the jersey number and date it was handed out.
/// Players can have multiple assignments (different kits, or a replacement after a lost jersey),
/// so this is an N:M join row, not a unique 1:1 link.
/// </summary>
public class PlayerUniformAssignment
{
    public int Id { get; set; }

    public int PlayerId { get; set; }
    public Player? Player { get; set; }

    public int UniformId { get; set; }
    public Uniform? Uniform { get; set; }

    /// <summary>Jersey number stamped on the back of this kit. Stored as a string so admins can
    /// record "10", "10A", or any non-numeric variant their stock uses without surgery later.</summary>
    [Required, MaxLength(16)]
    public string JerseyNumber { get; set; } = string.Empty;

    /// <summary>When the kit was physically handed to the family. Required on create so the admin
    /// table can show "given on" at a glance.</summary>
    public DateOnly AssignedAt { get; set; }

    /// <summary>Optional return date — set if the kit comes back (lost/returned/replaced).
    /// Active assignments leave this null; the admin UI hides returned rows by default.</summary>
    public DateOnly? ReturnedAt { get; set; }

    /// <summary>Free-form notes (size discrepancy, replacement reason, etc.).</summary>
    [MaxLength(500)]
    public string? Notes { get; set; }

    /// <summary>Identity user id of the admin who recorded the assignment. Null when the
    /// AspNetUsers row was deleted later; the assignment itself stays.</summary>
    [MaxLength(450)]
    public string? AssignedByUserId { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
