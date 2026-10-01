using System.ComponentModel.DataAnnotations;

namespace SoccerSchool.Api.Domain;

/// <summary>
/// Durable kid profile owned by a ParentAccount. Per-season fields
/// (grade, sizes, waiver) live on RegistrationPlayer.
/// </summary>
public class Player
{
    public int Id { get; set; }

    public int ParentAccountId { get; set; }
    public ParentAccount? ParentAccount { get; set; }

    [Required, MaxLength(80)]
    public string FirstName { get; set; } = string.Empty;

    [Required, MaxLength(80)]
    public string LastName { get; set; } = string.Empty;

    public DateOnly DateOfBirth { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>Set when the player is archived (left the club, or their family deleted its
    /// account). Archived players are hidden everywhere by a global query filter
    /// (<c>AppDbContext</c>): player lists, rosters, pickers, schedules, messaging, the parent
    /// app. Team memberships are kept, so unarchiving puts the player back where they were.
    /// Registration and invoice records still show them.</summary>
    public DateTime? ArchivedAt { get; set; }

    public PlayerArchiveReason? ArchivedReason { get; set; }

    /// <summary>Login name of the admin who archived the player (informational).</summary>
    [MaxLength(256)]
    public string? ArchivedBy { get; set; }
}

public enum PlayerArchiveReason
{
    /// <summary>An admin archived the player.</summary>
    Admin = 0,
    /// <summary>The family deleted its account. Undone automatically if the family signs up again.</summary>
    FamilyDeleted = 1,
}
