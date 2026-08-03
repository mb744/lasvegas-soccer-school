namespace SoccerSchool.Api;

public static class AuthSchemes
{
    /// <summary>The JWT bearer scheme used by the native mobile app. Registered alongside the
    /// default Identity cookie scheme so the web app is unaffected. Mobile endpoints opt in with
    /// <c>[Authorize(AuthenticationSchemes = AuthSchemes.MobileJwt)]</c>.</summary>
    public const string MobileJwt = "MobileJwt";
}
