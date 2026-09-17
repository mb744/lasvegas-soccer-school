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
    private readonly AppDbContext _db;

    public MobileAuthController(
        UserManager<ApplicationUser> users,
        SignInManager<ApplicationUser> signIn,
        IMobileTokenService tokens,
        AppDbContext db)
    {
        _users = users;
        _signIn = signIn;
        _tokens = tokens;
        _db = db;
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
    /// In-app account deletion, required by App Store Guideline 5.1.1(v) for any app with sign-in.
    /// Revokes every refresh token, deletes every push-notification device, drops chat memberships
    /// (their messages remain but the sender label is anonymized), permanently locks the login, and
    /// scrubs personal identifiers (email, name, phone) from the ApplicationUser and, if the caller
    /// owns a family, its ParentAccount. Player rosters, registrations, and invoices are retained
    /// as legitimate school business records.
    /// </summary>
    [HttpDelete("me")]
    [Authorize(AuthenticationSchemes = AuthSchemes.MobileJwt)]
    public async Task<IActionResult> DeleteMe(CancellationToken ct)
    {
        var user = await _users.GetUserAsync(User);
        if (user is null) return Unauthorized();
        var userId = user.Id;

        // 1. Revoke every active refresh token so the current session and any others die immediately.
        var refreshes = await _db.MobileRefreshTokens.Where(r => r.UserId == userId && r.RevokedAt == null).ToListAsync(ct);
        var revokedAt = DateTime.UtcNow;
        foreach (var r in refreshes) r.RevokedAt = revokedAt;

        // 2. Delete every push device — no more notifications after account deletion.
        var devices = await _db.DeviceTokens.Where(d => d.UserId == userId).ToListAsync(ct);
        _db.DeviceTokens.RemoveRange(devices);

        // 3. Drop chat memberships. Message history stays (other users saw those messages), but
        //    anonymize the sender name on messages authored by this user.
        var account = await _db.ParentAccounts.FirstOrDefaultAsync(p => p.UserId == userId, ct);
        var memberships = await _db.ChatGroupMembers
            .Where(m => m.UserId == userId || (account != null && m.ParentAccountId == account.Id))
            .ToListAsync(ct);
        _db.ChatGroupMembers.RemoveRange(memberships);

        var authored = await _db.ChatMessages.Where(m => m.SenderUserId == userId).ToListAsync(ct);
        foreach (var m in authored) m.SenderName = "Deleted user";

        // 4. Delete any pending block/report rows the user created — we don't retain them under a
        //    ghost identity.
        var blocks = await _db.ChatUserBlocks
            .Where(b => b.BlockerUserId == userId || b.BlockedUserId == userId)
            .ToListAsync(ct);
        _db.ChatUserBlocks.RemoveRange(blocks);

        // 5. If this user owns a family, anonymize the ParentAccount PII. Player + registration
        //    records intentionally survive (they're LVSS business records the family agreed to on
        //    enrollment). Collaborator rows are dropped so linked logins lose access.
        if (account is not null)
        {
            var collaborators = await _db.ParentAccountCollaborators
                .Where(c => c.ParentAccountId == account.Id)
                .ToListAsync(ct);
            _db.ParentAccountCollaborators.RemoveRange(collaborators);

            account.FirstName = "Deleted";
            account.LastName = "User";
            account.CellPhone = null;
            account.AddressLine1 = null;
            account.AddressLine2 = null;
            account.City = null;
            account.PostalCode = null;
            account.NoCommunications = true;
        }
        else
        {
            // Collaborator (linked to someone else's family) — just drop the link.
            var collabLinks = await _db.ParentAccountCollaborators.Where(c => c.UserId == userId).ToListAsync(ct);
            _db.ParentAccountCollaborators.RemoveRange(collabLinks);
        }

        await _db.SaveChangesAsync(ct);

        // 6. Anonymize the ApplicationUser and permanently lock it out. Overwriting the email with
        //    a namespaced sentinel prevents the address colliding with a future sign-up and stops
        //    any password-reset flow from re-activating this login.
        var sentinel = $"deleted-{userId}@removed.lvss.local";
        user.Email = sentinel;
        user.NormalizedEmail = _users.NormalizeEmail(sentinel);
        user.UserName = sentinel;
        user.NormalizedUserName = _users.NormalizeName(sentinel);
        user.PhoneNumber = null;
        user.PhoneNumberConfirmed = false;
        user.EmailConfirmed = false;
        user.LockoutEnabled = true;
        user.LockoutEnd = DateTimeOffset.MaxValue;
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
