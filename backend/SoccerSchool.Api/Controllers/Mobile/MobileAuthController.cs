using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SoccerSchool.Api.Data;
using SoccerSchool.Api.Domain;
using SoccerSchool.Api.Dtos;
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
    private readonly AppDbContext _db;
    private readonly ILogger<MobileAuthController> _logger;

    public MobileAuthController(
        UserManager<ApplicationUser> users,
        SignInManager<ApplicationUser> signIn,
        IMobileTokenService tokens,
        IAccountDeletionService deletion,
        IExternalIdentityService external,
        IReclaimHasher reclaim,
        AppDbContext db,
        ILogger<MobileAuthController> logger)
    {
        _users = users;
        _signIn = signIn;
        _tokens = tokens;
        _deletion = deletion;
        _external = external;
        _reclaim = reclaim;
        _db = db;
        _logger = logger;
    }

    [HttpPost("login")]
    public async Task<ActionResult<MobileTokenResponse>> Login([FromBody] MobileLoginRequest req, CancellationToken ct)
    {
        var user = await _users.FindByEmailAsync(req.Email);
        if (user is null)
            return Unauthorized("Invalid email or password.");

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

        // 3. Nothing yet — mint a fresh user + parent record (with reunion of any deleted family).
        if (user is null)
        {
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

        var pair = await _tokens.IssueAsync(user, ct);
        return Ok(await BuildTokenResponseAsync(user, pair, ct));
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
