using System.ComponentModel.DataAnnotations;

namespace SoccerSchool.Api.Domain;

/// <summary>
/// One install of the parent app, refreshed every time the app launches signed in (see
/// <c>POST /api/mobile/devices/check-in</c>). Unlike <see cref="DeviceToken"/>, a row exists even
/// when the parent declined notifications or push registration failed, so "has the app" and "last
/// seen" don't depend on push. The push fields record why a device has no token.
/// </summary>
public class MobileAppInstall
{
    public int Id { get; set; }

    /// <summary>Random id the app generates once per install and keeps in secure storage.</summary>
    [Required, MaxLength(64)]
    public string InstallationId { get; set; } = string.Empty;

    /// <summary>Login last signed in on this install (moves on a shared device).</summary>
    [Required, MaxLength(450)]
    public string UserId { get; set; } = string.Empty;
    public ApplicationUser? User { get; set; }

    public DevicePlatform Platform { get; set; } = DevicePlatform.Unknown;

    [MaxLength(32)]
    public string? AppVersion { get; set; }

    [MaxLength(32)]
    public string? BuildNumber { get; set; }

    [MaxLength(32)]
    public string? OsVersion { get; set; }

    /// <summary>Notification permission as the OS reports it: granted, denied or undetermined.</summary>
    [MaxLength(16)]
    public string? PushPermission { get; set; }

    /// <summary>True when the last check-in carried an Expo push token.</summary>
    public bool HasPushToken { get; set; }

    /// <summary>Why getting a push token failed on the last check-in, if it did.</summary>
    [MaxLength(500)]
    public string? PushError { get; set; }

    public DateTime FirstSeenAt { get; set; } = DateTime.UtcNow;
    public DateTime LastSeenAt { get; set; } = DateTime.UtcNow;
}
