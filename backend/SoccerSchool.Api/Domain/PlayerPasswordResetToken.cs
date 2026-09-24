using System.ComponentModel.DataAnnotations;

namespace SoccerSchool.Api.Domain;

/// <summary>
/// Single-use link emailed to the parent when a kid taps "Forgot password?" in the Daily Training
/// app. The parent opens <c>/reset-player-password?token=…</c> on the website and picks a new
/// password for their child. Only the SHA-256 hash of the token is stored.
/// </summary>
public class PlayerPasswordResetToken
{
    public int Id { get; set; }

    public int PlayerLoginId { get; set; }
    public PlayerLogin? PlayerLogin { get; set; }

    [Required, MaxLength(128)]
    public string TokenHash { get; set; } = string.Empty;

    public DateTime ExpiresAt { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? UsedAt { get; set; }

    public bool IsUsable => UsedAt is null && ExpiresAt > DateTime.UtcNow;
}
