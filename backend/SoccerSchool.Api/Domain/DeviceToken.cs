using System.ComponentModel.DataAnnotations;

namespace SoccerSchool.Api.Domain;

/// <summary>The platform a push token targets — drives nothing on our side today (Expo abstracts
/// APNs/FCM) but is stored for diagnostics and future direct-provider sends.</summary>
public enum DevicePlatform
{
    Unknown = 0,
    Ios = 1,
    Android = 2,
}

/// <summary>
/// An Expo push token registered by a parent's device. We send notifications (new chat message,
/// attendance reminder) to every active token for a user via the Expo Push API. One row per device;
/// re-registering the same token just refreshes <see cref="LastSeenAt"/>. Tokens Expo reports as
/// <c>DeviceNotRegistered</c> are deleted so we stop sending to uninstalled apps.
/// </summary>
public class DeviceToken
{
    public int Id { get; set; }

    [Required, MaxLength(450)]
    public string UserId { get; set; } = string.Empty;
    public ApplicationUser? User { get; set; }

    /// <summary>The Expo push token, e.g. <c>ExponentPushToken[xxxxxxxxxxxxxxxxxxxxxx]</c>.</summary>
    [Required, MaxLength(256)]
    public string ExpoPushToken { get; set; } = string.Empty;

    public DevicePlatform Platform { get; set; } = DevicePlatform.Unknown;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime LastSeenAt { get; set; } = DateTime.UtcNow;
}
