using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace SoccerSchool.Api.Services;

/// <summary>
/// Rules and helpers for Daily Training (kid) logins, shared by the parent-management, sign-in and
/// password-reset endpoints so they can't drift apart.
/// </summary>
public static partial class PlayerCredentials
{
    public const int MinUsernameLength = 3;
    public const int MaxUsernameLength = 32;

    /// <summary>Kids type these on a tablet — long enough to resist guessing given the lockout,
    /// short enough that a 7-year-old can remember it.</summary>
    public const int MinPasswordLength = 6;
    public const int MaxPasswordLength = 128;

    public const int MaxFailedAttempts = 5;
    public static readonly TimeSpan LockoutDuration = TimeSpan.FromMinutes(15);

    public static readonly TimeSpan ResetTokenLifetime = TimeSpan.FromHours(24);
    /// <summary>A second "forgot password" tap within this window doesn't send another email.</summary>
    public static readonly TimeSpan ResetEmailCooldown = TimeSpan.FromMinutes(10);

    [GeneratedRegex("^[a-z0-9._-]+$")]
    private static partial Regex UsernamePattern();

    public static string NormalizeUsername(string? username) => (username ?? string.Empty).Trim().ToLowerInvariant();

    /// <summary>Returns an error message, or null when the (already normalized) username is valid.</summary>
    public static string? ValidateUsername(string normalized)
    {
        if (normalized.Length < MinUsernameLength || normalized.Length > MaxUsernameLength)
            return $"Username must be {MinUsernameLength}–{MaxUsernameLength} characters.";
        if (!UsernamePattern().IsMatch(normalized))
            return "Username can only use letters, numbers, dots, dashes and underscores.";
        return null;
    }

    /// <summary>Returns an error message, or null when the password is acceptable.</summary>
    public static string? ValidatePassword(string? password)
    {
        if (string.IsNullOrEmpty(password) || password.Length < MinPasswordLength)
            return $"Password must be at least {MinPasswordLength} characters.";
        if (password.Length > MaxPasswordLength)
            return $"Password must be at most {MaxPasswordLength} characters.";
        return null;
    }

    /// <summary>URL-safe random token for refresh tokens and reset links.</summary>
    public static string GenerateToken(int bytes = 32) =>
        Convert.ToBase64String(RandomNumberGenerator.GetBytes(bytes))
            .TrimEnd('=').Replace('+', '-').Replace('/', '_');

    public static string HashToken(string value) =>
        Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
}
