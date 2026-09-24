using System.ComponentModel.DataAnnotations;

namespace SoccerSchool.Api.Domain;

/// <summary>
/// A kid's username/password for the Daily Training app. Deliberately NOT an Identity user:
/// kids have no email, must never be able to sign in to the parent/admin surfaces, and are fully
/// managed by their parent (create, rename, reset password, remove). Tokens are issued under the
/// separate <c>PlayerJwt</c> scheme (audience <c>lvss-training</c>), which no parent or admin
/// endpoint accepts. One login per player.
/// </summary>
public class PlayerLogin
{
    public int Id { get; set; }

    public int PlayerId { get; set; }
    public Player? Player { get; set; }

    /// <summary>Stored lower-cased (see <c>PlayerCredentials.NormalizeUsername</c>); unique.</summary>
    [Required, MaxLength(32)]
    public string Username { get; set; } = string.Empty;

    /// <summary>ASP.NET Identity <c>PasswordHasher</c> output (PBKDF2, versioned format).</summary>
    [Required, MaxLength(512)]
    public string PasswordHash { get; set; } = string.Empty;

    /// <summary>Consecutive failed sign-ins; reset on success or on a password change.</summary>
    public int AccessFailedCount { get; set; }

    /// <summary>When set and in the future, sign-in is refused (brute-force protection).</summary>
    public DateTime? LockoutEnd { get; set; }

    public DateTime? LastLoginAt { get; set; }

    /// <summary>Access tokens issued before this are rejected, so a parent's password change (or
    /// reset) signs the kid out everywhere immediately rather than when the JWT expires.</summary>
    public DateTime PasswordChangedAt { get; set; } = DateTime.UtcNow;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public List<PlayerRefreshToken> RefreshTokens { get; set; } = new();
}
