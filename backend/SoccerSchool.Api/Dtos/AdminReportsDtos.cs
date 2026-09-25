namespace SoccerSchool.Api.Dtos;

/// <summary>One row of the "who's using the mobile app" report. All timestamps are UTC.</summary>
public record MobileUsageRow(
    string UserId,
    string Email,
    string Name,
    int PlayerCount,
    /// <summary>True when at least one DeviceToken row exists for this user. The push-sender
    /// cleans up unregistered tokens, so a live row is a solid "the app is installed" signal.</summary>
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
    DateTime? AccountCreatedAt);
