using System.ComponentModel.DataAnnotations;

namespace SoccerSchool.Api.Domain;

/// <summary>
/// A long-lived refresh token issued to a mobile (native app) login. The web app uses cookie
/// auth and never touches this; native apps can't hold cookies cleanly, so they exchange
/// email/password for a short-lived JWT access token plus one of these. Only the SHA-256 hash of
/// the token is stored — the raw value lives only on the device. Rotated on every refresh
/// (the old row is revoked and a new one issued) so a stolen-and-replayed token is detectable.
/// </summary>
public class MobileRefreshToken
{
    public int Id { get; set; }

    [Required, MaxLength(450)]
    public string UserId { get; set; } = string.Empty;
    public ApplicationUser? User { get; set; }

    /// <summary>SHA-256 hash (Base64) of the opaque refresh token. The raw token is never stored.</summary>
    [Required, MaxLength(128)]
    public string TokenHash { get; set; } = string.Empty;

    public DateTime ExpiresAt { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>Set when the token is rotated (on refresh) or explicitly logged out. A revoked
    /// token can never be exchanged again.</summary>
    public DateTime? RevokedAt { get; set; }

    public bool IsActive => RevokedAt is null && ExpiresAt > DateTime.UtcNow;
}
