using System.Text.Json;
using Microsoft.Extensions.Options;
using SoccerSchool.Api.Options;

namespace SoccerSchool.Api.Services;

/// <summary>
/// Minimal user identity extracted from a third-party OAuth provider (Google, Facebook) after the
/// mobile app has already completed the browser flow. Used by the mobile auth controller to
/// find-or-create a matching <see cref="Domain.ApplicationUser"/> and issue app tokens.
/// </summary>
public record ExternalIdentity(string ProviderKey, string Email, string FirstName, string LastName);

/// <summary>Verifies the OAuth tokens the mobile app forwards after a Google or Facebook login,
/// so the backend can trust the caller's identity without running the browser flow itself.
/// Failures return null; the auth controller responds 401.</summary>
public interface IExternalIdentityService
{
    /// <summary>Validates a Google-issued <c>id_token</c> against Google's tokeninfo endpoint and
    /// checks the <c>aud</c> claim matches one of the configured client IDs.</summary>
    Task<ExternalIdentity?> VerifyGoogleAsync(string idToken, CancellationToken ct);

    /// <summary>Validates a Facebook user access token by calling <c>debug_token</c> (checks the
    /// token belongs to our app + is unexpired) then fetches the user's profile fields.</summary>
    Task<ExternalIdentity?> VerifyFacebookAsync(string accessToken, CancellationToken ct);
}

public class ExternalIdentityService : IExternalIdentityService
{
    private readonly HttpClient _http;
    private readonly AppOptions.OAuthOptions _oauth;
    private readonly ILogger<ExternalIdentityService> _logger;

    public ExternalIdentityService(HttpClient http, IOptions<AppOptions> app, ILogger<ExternalIdentityService> logger)
    {
        _http = http;
        _oauth = app.Value.OAuth;
        _logger = logger;
    }

    public async Task<ExternalIdentity?> VerifyGoogleAsync(string idToken, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(idToken)) return null;

        // Google's tokeninfo endpoint validates signature/expiry/issuer server-side and returns
        // the decoded claims. Cheap for our low-QPS auth path; avoids importing Google.Apis.Auth.
        var url = "https://oauth2.googleapis.com/tokeninfo?id_token=" + Uri.EscapeDataString(idToken);
        HttpResponseMessage resp;
        try { resp = await _http.GetAsync(url, ct); }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Google tokeninfo request failed.");
            return null;
        }
        if (!resp.IsSuccessStatusCode)
        {
            _logger.LogInformation("Google tokeninfo rejected: {Status}", resp.StatusCode);
            return null;
        }

        using var doc = await JsonDocument.ParseAsync(await resp.Content.ReadAsStreamAsync(ct), cancellationToken: ct);
        var root = doc.RootElement;

        var aud = root.TryGetProperty("aud", out var a) ? a.GetString() : null;
        var accepted = _oauth.Google.AcceptedAudiences().ToHashSet(StringComparer.Ordinal);
        if (aud is null || !accepted.Contains(aud))
        {
            _logger.LogInformation("Google token audience {Aud} not in accepted list.", aud);
            return null;
        }

        var iss = root.TryGetProperty("iss", out var i) ? i.GetString() : null;
        if (iss is not ("accounts.google.com" or "https://accounts.google.com"))
        {
            _logger.LogInformation("Google token issuer {Iss} not accepted.", iss);
            return null;
        }

        // email_verified comes back as a string "true"/"false", not a bool.
        var verified = root.TryGetProperty("email_verified", out var v) && v.ValueKind == JsonValueKind.String
            ? string.Equals(v.GetString(), "true", StringComparison.OrdinalIgnoreCase)
            : (v.ValueKind == JsonValueKind.True);
        if (!verified)
        {
            _logger.LogInformation("Google token email not verified; refusing.");
            return null;
        }

        var sub = root.TryGetProperty("sub", out var s) ? s.GetString() : null;
        var email = root.TryGetProperty("email", out var e) ? e.GetString() : null;
        if (string.IsNullOrWhiteSpace(sub) || string.IsNullOrWhiteSpace(email)) return null;

        var first = root.TryGetProperty("given_name", out var f) ? f.GetString() : null;
        var last = root.TryGetProperty("family_name", out var l) ? l.GetString() : null;
        return new ExternalIdentity(sub!, email!, first ?? "", last ?? "");
    }

    public async Task<ExternalIdentity?> VerifyFacebookAsync(string accessToken, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(accessToken)) return null;
        if (!_oauth.Facebook.IsConfigured) return null;

        var appToken = $"{_oauth.Facebook.AppId}|{_oauth.Facebook.AppSecret}";

        // 1. debug_token confirms the token belongs to our app and hasn't expired.
        var debugUrl = $"https://graph.facebook.com/debug_token?input_token={Uri.EscapeDataString(accessToken)}&access_token={Uri.EscapeDataString(appToken)}";
        HttpResponseMessage debugResp;
        try { debugResp = await _http.GetAsync(debugUrl, ct); }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Facebook debug_token request failed.");
            return null;
        }
        if (!debugResp.IsSuccessStatusCode) return null;

        using (var debugDoc = await JsonDocument.ParseAsync(await debugResp.Content.ReadAsStreamAsync(ct), cancellationToken: ct))
        {
            if (!debugDoc.RootElement.TryGetProperty("data", out var data)) return null;
            var isValid = data.TryGetProperty("is_valid", out var v) && v.GetBoolean();
            var tokenAppId = data.TryGetProperty("app_id", out var app) ? app.GetString() : null;
            if (!isValid || tokenAppId != _oauth.Facebook.AppId)
            {
                _logger.LogInformation("Facebook token invalid or wrong app_id ({AppId}).", tokenAppId);
                return null;
            }
        }

        // 2. Fetch the user's profile fields.
        var meUrl = $"https://graph.facebook.com/me?fields=id,email,first_name,last_name&access_token={Uri.EscapeDataString(accessToken)}";
        HttpResponseMessage meResp;
        try { meResp = await _http.GetAsync(meUrl, ct); }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Facebook /me request failed.");
            return null;
        }
        if (!meResp.IsSuccessStatusCode) return null;

        using var meDoc = await JsonDocument.ParseAsync(await meResp.Content.ReadAsStreamAsync(ct), cancellationToken: ct);
        var root = meDoc.RootElement;
        var fbId = root.TryGetProperty("id", out var idEl) ? idEl.GetString() : null;
        var email = root.TryGetProperty("email", out var emailEl) ? emailEl.GetString() : null;
        // Some Facebook users decline the email permission; without an email we can't merge with
        // existing parent records, so we refuse the login rather than create orphan accounts.
        if (string.IsNullOrWhiteSpace(fbId) || string.IsNullOrWhiteSpace(email)) return null;

        var first = root.TryGetProperty("first_name", out var fEl) ? fEl.GetString() : null;
        var last = root.TryGetProperty("last_name", out var lEl) ? lEl.GetString() : null;
        return new ExternalIdentity(fbId!, email!, first ?? "", last ?? "");
    }
}
