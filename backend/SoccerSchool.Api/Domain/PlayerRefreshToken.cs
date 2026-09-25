using System.ComponentModel.DataAnnotations;

namespace SoccerSchool.Api.Domain;

/// <summary>
/// Refresh token for a <see cref="PlayerLogin"/> (Daily Training app). Same model as
/// <see cref="MobileRefreshToken"/> — only the SHA-256 hash is stored, rotated on every refresh —
/// but kept in its own table so kid sessions can never be confused with parent/admin sessions.
/// </summary>
public class PlayerRefreshToken
{
    public int Id { get; set; }

    public int PlayerLoginId { get; set; }
    public PlayerLogin? PlayerLogin { get; set; }

    /// <summary>SHA-256 hash (Base64) of the opaque refresh token. The raw token is never stored.</summary>
    [Required, MaxLength(128)]
    public string TokenHash { get; set; } = string.Empty;

    public DateTime ExpiresAt { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>Set when rotated, logged out, or when the parent changes/removes the login.</summary>
    public DateTime? RevokedAt { get; set; }

    public bool IsActive => RevokedAt is null && ExpiresAt > DateTime.UtcNow;
}
