using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SoccerSchool.Api.Data;
using SoccerSchool.Api.Domain;
using SoccerSchool.Api.Dtos;
using SoccerSchool.Api.Options;
using SoccerSchool.Api.Services;

namespace SoccerSchool.Api.Controllers.Mobile;

/// <summary>
/// Token auth for the native mobile app. Mirrors <see cref="AuthController"/>'s password check but
/// issues a JWT access token + opaque refresh token instead of setting the web auth cookie. The web
/// app is untouched. All protected mobile endpoints authenticate against <see cref="AuthSchemes.MobileJwt"/>.
/// </summary>
[ApiController]
[Route("api/mobile/auth")]
public class MobileAuthController : ControllerBase
{
    private readonly UserManager<ApplicationUser> _users;
    private readonly SignInManager<ApplicationUser> _signIn;
    private readonly IMobileTokenService _tokens;
    private readonly IAccountDeletionService _deletion;
    private readonly IExternalIdentityService _external;
    private readonly IReclaimHasher _reclaim;
    private readonly IEmailSender _emailSender;
    private readonly AppOptions _app;
    private readonly AppDbContext _db;
    private readonly ILogger<MobileAuthController> _logger;

    public MobileAuthController(
        UserManager<ApplicationUser> users,
        SignInManager<ApplicationUser> signIn,
        IMobileTokenService tokens,
        IAccountDeletionService deletion,
        IExternalIdentityService external,
        IReclaimHasher reclaim,
        IEmailSender emailSender,
        IOptions<AppOptions> app,
        AppDbContext db,
        ILogger<MobileAuthController> logger)
    {
        _users = users;
        _signIn = signIn;
        _tokens = tokens;
        _deletion = deletion;
        _external = external;
        _reclaim = reclaim;
        _emailSender = emailSender;
        _app = app.Value;
        _db = db;
        _logger = logger;
    }

    [HttpPost("login")]
    public async Task<ActionResult<MobileTokenResponse>> Login([FromBody] MobileLoginRequest req, CancellationToken ct)
    {
        var user = await _users.FindByEmailAsync(req.Email);
        if (user is null)
            return Unauthorized("Invalid email or password.");

        // A parent who signed up on the web via Google/Facebook has no password set. Rather than
        // returning a generic "invalid email or password", steer them to the right button so they
        // don't keep hammering the password field.
        if (!await _users.HasPasswordAsync(user))
        {
            var logins = await _users.GetLoginsAsync(user);
            var provider = logins.Select(l => l.LoginProvider).FirstOrDefault();
            var providerHint = provider is null
                ? "This account signs in with a social provider — use the Continue with Google or Facebook button."
                : $"This account signs in with {provider} — tap Continue with {provider}.";
            return Unauthorized(providerHint);
        }

        // No cookie sign-in — just verify the password (with lockout) and mint tokens.
        var check = await _signIn.CheckPasswordSignInAsync(user, req.Password, lockoutOnFailure: true);
        if (!check.Succeeded)
            return Unauthorized(check.IsLockedOut ? "Account locked. Try again later." : "Invalid email or password.");

        user.LastLoginAt = DateTime.UtcNow;
        await _users.UpdateAsync(user);

        var pair = await _tokens.IssueAsync(user, ct);
        return Ok(await BuildTokenResponseAsync(user, pair, ct));
    }

    [HttpPost("refresh")]
    public async Task<ActionResult<MobileTokenResponse>> Refresh([FromBody] MobileRefreshRequest req, CancellationToken ct)
    {
        var pair = await _tokens.RefreshAsync(req.RefreshToken, ct);
        if (pair is null) return Unauthorized("Session expired. Please sign in again.");

        // RefreshAsync rotated the token for some user; recover who from the new access token's
        // `sub` claim (we just signed it) so we can rebuild the profile payload.
        var userId = ReadSubject(pair.AccessToken);
        var user = userId is null ? null : await _users.FindByIdAsync(userId);
        if (user is null) return Unauthorized("Session expired. Please sign in again.");

        return Ok(await BuildTokenResponseAsync(user, pair, ct));
    }

    [HttpPost("logout")]
    public async Task<IActionResult> Logout([FromBody] MobileRefreshRequest req, CancellationToken ct)
    {
        await _tokens.RevokeAsync(req.RefreshToken, ct);
        return NoContent();
    }

    /// <summary>Exchange a Google id_token (obtained by the mobile app via expo-auth-session) for
    /// LVSS mobile tokens. New Google logins are matched to an existing ApplicationUser by email,
    /// or a new user + ParentAccount is created; the ReclaimEmailHash reunion still fires.</summary>
    [HttpPost("google")]
    public async Task<ActionResult<MobileTokenResponse>> Google([FromBody] MobileExternalTokenRequest req, CancellationToken ct)
    {
        var identity = await _external.VerifyGoogleAsync(req.Token, ct);
        if (identity is null) return Unauthorized("Google sign-in failed.");
        return await SignInExternalAsync("Google", identity, ct);
    }

    /// <summary>Exchange a Facebook user access token for LVSS mobile tokens. Same
    /// find-or-create semantics as the Google endpoint.</summary>
    [HttpPost("facebook")]
    public async Task<ActionResult<MobileTokenResponse>> Facebook([FromBody] MobileExternalTokenRequest req, CancellationToken ct)
    {
        var identity = await _external.VerifyFacebookAsync(req.Token, ct);
        if (identity is null) return Unauthorized("Facebook sign-in failed.");
        return await SignInExternalAsync("Facebook", identity, ct);
    }

    private async Task<ActionResult<MobileTokenResponse>> SignInExternalAsync(
        string provider, ExternalIdentity identity, CancellationToken ct)
    {
        // 1. Existing external link? Sign that user in directly.
        var user = await _users.FindByLoginAsync(provider, identity.ProviderKey);

        // 2. Otherwise match by email — an existing password-login user, or a reclaim-eligible
        //    ghost family, can be reconnected without spawning a duplicate.
        if (user is null && !string.IsNullOrWhiteSpace(identity.Email))
        {
            user = await _users.FindByEmailAsync(identity.Email);
        }

        // Track whether we spawn a genuinely new account this call so the admin notification only
        // fires on real first-time signups, not every OAuth re-login.
        var isFreshSignup = false;
        var reunionAccountId = (int?)null;

        // 3. Nothing yet — mint a fresh user + parent record (with reunion of any deleted family).
        if (user is null)
        {
            isFreshSignup = true;
            user = new ApplicationUser
            {
                UserName = identity.Email,
                Email = identity.Email,
                EmailConfirmed = true,
            };
            var create = await _users.CreateAsync(user);
            if (!create.Succeeded)
            {
                _logger.LogWarning("Mobile external create failed: {Errors}", string.Join(",", create.Errors.Select(e => e.Description)));
                return Unauthorized("Could not create account.");
            }

            var reclaimHash = _reclaim.Hash(identity.Email);
            var account = reclaimHash is null
                ? null
                : await _db.ParentAccounts.FirstOrDefaultAsync(p => p.ReclaimEmailHash == reclaimHash, ct);

            if (account is not null)
            {
                account.UserId = user.Id;
                account.FirstName = string.IsNullOrWhiteSpace(identity.FirstName) ? account.FirstName : identity.FirstName;
                account.LastName = string.IsNullOrWhiteSpace(identity.LastName) ? account.LastName : identity.LastName;
                account.ReclaimEmailHash = null;
                account.NoCommunications = false;
                reunionAccountId = account.Id;
                _logger.LogInformation("Reunited external signup {Email} with previously-deleted family {AccountId}.", identity.Email, account.Id);
            }
            else
            {
                _db.ParentAccounts.Add(new ParentAccount
                {
                    UserId = user.Id,
                    FirstName = identity.FirstName,
                    LastName = identity.LastName,
                    Language = Language.English,
                });
            }
            await _db.SaveChangesAsync(ct);
        }

        // Refuse users who deleted their account (login is anonymized + locked).
        if (await _users.IsLockedOutAsync(user))
            return Unauthorized("Account locked.");

        // Ensure the external login is linked (idempotent — silently swallows the AlreadyLinked error).
        var linkResult = await _users.AddLoginAsync(user, new Microsoft.AspNetCore.Identity.UserLoginInfo(provider, identity.ProviderKey, provider));
        if (!linkResult.Succeeded && !linkResult.Errors.Any(e => e.Code == "LoginAlreadyAssociated"))
        {
            _logger.LogWarning("AddLogin failed for {Email}: {Errors}", identity.Email, string.Join(",", linkResult.Errors.Select(e => e.Description)));
        }

        user.LastLoginAt = DateTime.UtcNow;
        await _users.UpdateAsync(user);

        if (isFreshSignup)
        {
            // Fire-and-forget notification — never block the sign-in on ACS latency or failure.
            _ = NotifyAdminOfSignupAsync(
                $"Mobile ({provider})",
                identity.Email,
                $"{identity.FirstName} {identity.LastName}".Trim(),
                reunionAccountId,
                CancellationToken.None);
        }

        var pair = await _tokens.IssueAsync(user, ct);
        return Ok(await BuildTokenResponseAsync(user, pair, ct));
    }

    /// <summary>Emails the configured admin bootstrap address a heads-up whenever a new parent
    /// account is minted. Called only after the sign-up path — normal re-logins are quiet.
    /// Failures are logged and swallowed so a flaky email service never blocks a working sign-in.</summary>
    private async Task NotifyAdminOfSignupAsync(
        string channel, string email, string displayName, int? reunionAccountId, CancellationToken ct)
    {
        var adminEmail = _app.Admin.Email;
        if (string.IsNullOrWhiteSpace(adminEmail) || !_emailSender.IsAvailable) return;

        try
        {
            var subject = $"New LVSS signup: {(string.IsNullOrWhiteSpace(displayName) ? email : displayName)}";
            var body =
                "A new parent just signed up.\n\n" +
                $"Name: {(string.IsNullOrWhiteSpace(displayName) ? "(not provided)" : displayName)}\n" +
                $"Email: {email}\n" +
                $"Channel: {channel}\n" +
                (reunionAccountId is int rid
                    ? $"Reunion: reconnected to previously-deleted family #{rid} via ReclaimEmailHash.\n"
                    : "New family record created.\n") +
                "\nSee /admin/users on the admin site for the full profile.";
            var send = await _emailSender.SendAsync(adminEmail, subject, body, ct);
            if (!send.Success)
                _logger.LogWarning("Admin signup notification failed for {Email}: {Message}", email, send.Message);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Admin signup notification threw for {Email}.", email);
        }
    }

    [HttpGet("me")]
    [Authorize(AuthenticationSchemes = AuthSchemes.MobileJwt)]
    public async Task<ActionResult<MobileMeResponse>> Me(CancellationToken ct)
    {
        var user = await _users.GetUserAsync(User);
        if (user is null) return Unauthorized();
        return Ok(await BuildMeAsync(user, ct));
    }

    /// <summary>
    /// In-app account deletion, required by App Store Guideline 5.1.1(v). Runs the full teardown
    /// via <see cref="IAccountDeletionService.PurgeAsync"/>: revoke tokens, delete push devices,
    /// drop chat memberships, anonymize authored messages, scrub PII on ApplicationUser and (if
    /// owned) ParentAccount, permanently lock the login. Player and registration records stay
    /// intact as legitimate school business records. Before scrubbing, PurgeAsync also stamps a
    /// one-way reclaim hash on the ParentAccount so a future signup with the same email can be
    /// auto-linked to the same family (see <c>AuthController.Signup</c>).
    /// </summary>
    [HttpDelete("me")]
    [Authorize(AuthenticationSchemes = AuthSchemes.MobileJwt)]
    public async Task<IActionResult> DeleteMe(CancellationToken ct)
    {
        var user = await _users.GetUserAsync(User);
        if (user is null) return Unauthorized();

        await _deletion.PurgeAsync(user.Id, ct);
        return NoContent();
    }

    private async Task<MobileTokenResponse> BuildTokenResponseAsync(ApplicationUser user, TokenPair pair, CancellationToken ct)
    {
        var me = await BuildMeAsync(user, ct);
        return new MobileTokenResponse(
            pair.AccessToken, pair.AccessTokenExpiresAt,
            pair.RefreshToken, pair.RefreshTokenExpiresAt, me);
    }

    private async Task<MobileMeResponse> BuildMeAsync(ApplicationUser user, CancellationToken ct)
    {
        var account = await _db.ParentAccounts.FirstOrDefaultAsync(p => p.UserId == user.Id, ct);
        var roles = await _users.GetRolesAsync(user);

        var players = account is null
            ? new List<MobilePlayerDto>()
            : await _db.Players
                .Where(p => p.ParentAccountId == account.Id)
                .OrderBy(p => p.FirstName).ThenBy(p => p.LastName)
                .Select(p => new MobilePlayerDto(
                    p.Id, p.FirstName, p.LastName, p.DateOfBirth,
                    _db.TeamPlayers
                        .Where(tp => tp.PlayerId == p.Id)
                        .Select(tp => new MobilePlayerTeamDto(tp.TeamId, tp.Team!.Name))
                        .ToList()))
                .ToListAsync(ct);

        return new MobileMeResponse(
            user.Id,
            user.Email ?? "",
            account?.FirstName ?? "",
            account?.LastName ?? "",
            account?.CellPhone,
            account?.Language ?? Language.English,
            roles.Contains(Roles.Admin),
            players);
    }

    /// <summary>Pulls the <c>sub</c> claim out of a freshly-minted access token without re-validating
    /// it (we just signed it). Lets refresh rebuild the profile payload for the rotated user.</summary>
    private static string? ReadSubject(string accessToken)
    {
        try
        {
            var handler = new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler();
            var token = handler.ReadJwtToken(accessToken);
            return token.Subject;
        }
        catch
        {
            return null;
        }
    }
}
