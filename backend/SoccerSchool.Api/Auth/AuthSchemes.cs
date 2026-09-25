namespace SoccerSchool.Api;

public static class AuthSchemes
{
    /// <summary>The JWT bearer scheme used by the native mobile app. Registered alongside the
    /// default Identity cookie scheme so the web app is unaffected. Mobile endpoints opt in with
    /// <c>[Authorize(AuthenticationSchemes = AuthSchemes.MobileJwt)]</c>.</summary>
    public const string MobileJwt = "MobileJwt";

    /// <summary>ASP.NET Identity's default cookie scheme name. Kept explicit so admin controllers
    /// can accept BOTH the web cookie and the mobile JWT with
    /// <c>[Authorize(AuthenticationSchemes = AuthSchemes.CookieOrMobileJwt)]</c>.</summary>
    public const string IdentityCookie = "Identity.Application";

    /// <summary>Combined scheme string that accepts either the web cookie or the mobile JWT.
    /// Used on admin endpoints the mobile app needs to call (announcements CRUD, etc.).</summary>
    public const string CookieOrMobileJwt = IdentityCookie + "," + MobileJwt;

    /// <summary>JWT bearer scheme for kids signed in to the Daily Training app. Uses a different
    /// audience (<c>lvss-training</c>) than <see cref="MobileJwt"/>, so a kid's token is rejected by
    /// every parent/admin endpoint and a parent's token is rejected by the training endpoints.</summary>
    public const string PlayerJwt = "PlayerJwt";
}
