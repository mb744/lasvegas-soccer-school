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
    private readonly AppDbContext _db;
    private readonly AppOptions.AccountDeletionOptions _deletionOptions;

    public MobileAuthController(
        UserManager<ApplicationUser> users,
        SignInManager<ApplicationUser> signIn,
        IMobileTokenService tokens,
        AppDbContext db,
        IOptions<AppOptions> app)
    {
        _users = users;
        _signIn = signIn;
        _tokens = tokens;
        _db = db;
        _deletionOptions = app.Value.AccountDeletion;
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

    [HttpGet("me")]
    [Authorize(AuthenticationSchemes = AuthSchemes.MobileJwt)]
    public async Task<ActionResult<MobileMeResponse>> Me(CancellationToken ct)
    {
        var user = await _users.GetUserAsync(User);
        if (user is null) return Unauthorized();
        return Ok(await BuildMeAsync(user, ct));
    }

    /// <summary>
    /// Schedules the user's account for deletion after a configurable grace window (default 30 days,
    /// see <see cref="AppOptions.AccountDeletionOptions"/>). Immediately revokes every refresh token
    /// and deletes every push device so the "signed out on every device" contract holds, but leaves
    /// the login and PII intact so the user can sign back in and cancel with
    /// <c>POST cancel-deletion</c>. When the window elapses, <see cref="AccountPurgeJob"/> runs the
    /// hard anonymization via <see cref="IAccountDeletionService.PurgeAsync"/>. Required by App
    /// Store Guideline 5.1.1(v).
    /// </summary>
    [HttpDelete("me")]
    [Authorize(AuthenticationSchemes = AuthSchemes.MobileJwt)]
    public async Task<ActionResult<MobileScheduleDeletionResponse>> DeleteMe(CancellationToken ct)
    {
        var user = await _users.GetUserAsync(User);
        if (user is null) return Unauthorized();
        var userId = user.Id;

        // Idempotent: if a deletion is already pending, return the existing timestamp without
        // resetting the window (otherwise repeated taps would keep pushing the effective date out).
        if (user.PendingDeletionAt is DateTime existing)
        {
            return Ok(new MobileScheduleDeletionResponse(existing));
        }

        var graceDays = Math.Max(1, _deletionOptions.GraceDays);
        var pendingAt = DateTime.UtcNow.AddDays(graceDays);
        user.PendingDeletionAt = pendingAt;
        await _users.UpdateAsync(user);

        // Sign the user out on every device immediately — Apple expects the "signed out" contract
        // to hold from the moment they tap Delete, even though the anonymization waits.
        var refreshes = await _db.MobileRefreshTokens.Where(r => r.UserId == userId && r.RevokedAt == null).ToListAsync(ct);
        var revokedAt = DateTime.UtcNow;
        foreach (var r in refreshes) r.RevokedAt = revokedAt;

        // Stop sending push notifications right away — the account is on its way out.
        var devices = await _db.DeviceTokens.Where(d => d.UserId == userId).ToListAsync(ct);
        _db.DeviceTokens.RemoveRange(devices);

        await _db.SaveChangesAsync(ct);
        return Ok(new MobileScheduleDeletionResponse(pendingAt));
    }

    /// <summary>Cancels a pending account deletion. Must be called before the grace window elapses;
    /// after the AccountPurgeJob has run the login is anonymized and can no longer sign in.</summary>
    [HttpPost("cancel-deletion")]
    [Authorize(AuthenticationSchemes = AuthSchemes.MobileJwt)]
    public async Task<IActionResult> CancelDeletion(CancellationToken ct)
    {
        var user = await _users.GetUserAsync(User);
        if (user is null) return Unauthorized();

        if (user.PendingDeletionAt is null) return NoContent();

        user.PendingDeletionAt = null;
        await _users.UpdateAsync(user);
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
            players,
            user.PendingDeletionAt);
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
