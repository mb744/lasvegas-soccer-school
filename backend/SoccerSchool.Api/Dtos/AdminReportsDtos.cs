namespace SoccerSchool.Api.Dtos;

/// <summary>One row of the "who's using the mobile app" report. All timestamps are UTC.</summary>
public record MobileUsageRow(
    string UserId,
    string Email,
    string Name,
    int PlayerCount,
    /// <summary>True when the user has signed into the parent app: an install check-in, a push
    /// token or a mobile session. Doesn't depend on notifications being allowed.</summary>
    bool HasMobileApp,
    DateTime? FirstInstalledAt,
    DateTime? LastSeenAt,
    /// <summary>Newest MobileRefreshToken.CreatedAt regardless of revocation — captures the last
    /// time this user actually opened a mobile session, not just kept the app installed.</summary>
    DateTime? LastMobileLoginAt,
    bool HasIos,
    bool HasAndroid,
    int DeviceCount,
    /// <summary>Overall last login (web cookie OR mobile JWT). Useful for the "has never logged in
    /// anywhere" case where the family registered on the web but never signed back in.</summary>
    DateTime? LastLoginAt,
    /// <summary>ParentAccount.CreatedAt — the moment the family opened its account.</summary>
    DateTime? AccountCreatedAt,
    /// <summary>True when at least one device has a live push token (notifications reach them).</summary>
    bool PushEnabled,
    /// <summary>From the newest install check-in: granted / denied / undetermined. Null when the
    /// user's app is too old to check in.</summary>
    string? PushPermission,
    /// <summary>Why the newest install couldn't get a push token, if it couldn't.</summary>
    string? PushError,
    /// <summary>App version on the newest install check-in, e.g. "1.4.0 (31)".</summary>
    string? AppVersion);
