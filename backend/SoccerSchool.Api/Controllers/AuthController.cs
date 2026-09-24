using Microsoft.AspNetCore.Authentication;
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

namespace SoccerSchool.Api.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly UserManager<ApplicationUser> _users;
    private readonly SignInManager<ApplicationUser> _signIn;
    private readonly AppDbContext _db;
    private readonly AppOptions _app;
    private readonly IReclaimHasher _reclaim;
    private readonly IEmailSender _email;
    private readonly ICoachScopeService _coaches;
    private readonly ILogger<AuthController> _logger;

    public AuthController(
        UserManager<ApplicationUser> users,
        SignInManager<ApplicationUser> signIn,
        AppDbContext db,
        IOptions<AppOptions> app,
        IReclaimHasher reclaim,
        IEmailSender email,
        ICoachScopeService coaches,
        ILogger<AuthController> logger)
    {
        _users = users;
        _signIn = signIn;
        _db = db;
        _app = app.Value;
        _reclaim = reclaim;
        _email = email;
        _coaches = coaches;
        _logger = logger;
    }

    [HttpGet("providers")]
    [AllowAnonymous]
    public ActionResult<IEnumerable<string>> Providers()
    {
        var list = new List<string>();
        if (_app.OAuth.Google.IsConfigured) list.Add("Google");
        if (_app.OAuth.Facebook.IsConfigured) list.Add("Facebook");
        return Ok(list);
    }

    [HttpPost("signup")]
    public async Task<ActionResult<MeResponse>> Signup([FromBody] SignupRequest req, CancellationToken ct)
    {
        var existing = await _users.FindByEmailAsync(req.Email);
        if (existing is not null)
            return Conflict("An account with that email already exists.");

        var user = new ApplicationUser
        {
            UserName = req.Email,
            Email = req.Email,
            PhoneNumber = req.Phone,
            EmailConfirmed = false
        };
        var create = await _users.CreateAsync(user, req.Password);
        if (!create.Succeeded)
            return BadRequest(string.Join(" ", create.Errors.Select(e => e.Description)));

        // Auto-reclaim: if this email previously belonged to a mobile account that was deleted,
        // its ParentAccount was anonymized and stamped with a one-way ReclaimEmailHash pointing
        // back to this address. Repoint that ghost account (players, registrations, invoices)
        // to the fresh login instead of creating an empty new family.
        var reclaimHash = _reclaim.Hash(req.Email);
        ParentAccount? account = reclaimHash is null
            ? null
            : await _db.ParentAccounts.FirstOrDefaultAsync(p => p.ReclaimEmailHash == reclaimHash, ct);

        if (account is not null)
        {
            account.UserId = user.Id;
            account.FirstName = req.FirstName.Trim();
            account.LastName = req.LastName.Trim();
            account.CellPhone = PhoneNormalizer.Normalize(req.Phone);
            account.Language = req.Language;
            account.NoCommunications = false;
            account.ReclaimEmailHash = null;
            _logger.LogInformation("Reunited signup {Email} with previously-deleted family {AccountId}.", req.Email, account.Id);
        }
        else
        {
            account = new ParentAccount
            {
                UserId = user.Id,
                FirstName = req.FirstName.Trim(),
                LastName = req.LastName.Trim(),
                CellPhone = PhoneNormalizer.Normalize(req.Phone),
                Language = req.Language
            };
            _db.ParentAccounts.Add(account);
        }
        await _db.SaveChangesAsync(ct);

        await AttributeOutreachAsync(req.Email, req.Phone, account.Id, OutreachStatus.AccountCreated, ct);

        await _signIn.SignInAsync(user, isPersistent: true);
        await StampLastLoginAsync(user);
        return Ok(await BuildMeAsync(user, account));
    }

    [HttpPost("login")]
    public async Task<ActionResult<MeResponse>> Login([FromBody] LoginRequest req, CancellationToken ct)
    {
        var user = await _users.FindByEmailAsync(req.Email);
        if (user is null)
            return Unauthorized("Invalid email or password.");

        var result = await _signIn.PasswordSignInAsync(user, req.Password, req.RememberMe, lockoutOnFailure: true);
        if (!result.Succeeded)
            return Unauthorized(result.IsLockedOut ? "Account locked. Try again later." : "Invalid email or password.");

        await StampLastLoginAsync(user);
        var account = await _db.ParentAccounts.FirstOrDefaultAsync(p => p.UserId == user.Id, ct);
        return Ok(await BuildMeAsync(user, account));
    }

    private async Task StampLastLoginAsync(ApplicationUser user)
    {
        user.LastLoginAt = DateTime.UtcNow;
        await _users.UpdateAsync(user);
    }

    [HttpPost("logout")]
    public async Task<IActionResult> Logout()
    {
        await _signIn.SignOutAsync();
        return NoContent();
    }

    /// <summary>
    /// Password reset — step 1. User submits their email; if a matching account exists we mint an
    /// Identity reset token and email a link that opens the web reset page with the token pre-filled.
    /// Always returns 204 regardless of whether the email is on file so an attacker can't use this
    /// endpoint to enumerate registered addresses.
    /// </summary>
    [HttpPost("forgot-password")]
    [AllowAnonymous]
    public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordRequest req, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.Email)) return NoContent();
        var user = await _users.FindByEmailAsync(req.Email.Trim());
        // Silently no-op for missing/locked/OAuth-only accounts — mirrored 204 hides the state.
        if (user is null || await _users.IsLockedOutAsync(user)) return NoContent();
        // OAuth-only accounts have no password to reset — nothing to email.
        if (!await _users.HasPasswordAsync(user)) return NoContent();

        var token = await _users.GeneratePasswordResetTokenAsync(user);
        var baseUrl = (_app.PublicBaseUrl ?? string.Empty).TrimEnd('/');
        var link = $"{baseUrl}/reset-password?email={Uri.EscapeDataString(req.Email.Trim())}&token={Uri.EscapeDataString(token)}";

        var subject = "Reset your Las Vegas Soccer School password";
        var body =
            $"Hi,\n\n" +
            "Someone (hopefully you) asked to reset the password on your Las Vegas Soccer School account. " +
            "Open this link within the next hour to choose a new one:\n\n" +
            $"{link}\n\n" +
            "If you didn't request this, just ignore this email — your password stays the same.\n\n" +
            "Thanks,\nLas Vegas Soccer School";

        var send = await _email.SendAsync(req.Email.Trim(), subject, body, ct);
        if (!send.Success)
            _logger.LogWarning("Password reset email send failed for {Email}: {Message}", req.Email, send.Message);
        return NoContent();
    }

    /// <summary>Password reset — step 2. User submits the email + token from the link and their
    /// chosen new password. Token is single-use and time-limited by ASP.NET Identity.</summary>
    [HttpPost("reset-password")]
    [AllowAnonymous]
    public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordRequest req, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.Email) || string.IsNullOrWhiteSpace(req.Token) || string.IsNullOrWhiteSpace(req.NewPassword))
            return BadRequest("Missing required fields.");

        var user = await _users.FindByEmailAsync(req.Email.Trim());
        if (user is null) return BadRequest("Invalid reset link. Request a new one.");
        if (await _users.IsLockedOutAsync(user)) return BadRequest("Account locked.");

        var result = await _users.ResetPasswordAsync(user, req.Token, req.NewPassword);
        if (!result.Succeeded)
            return BadRequest(string.Join(" ", result.Errors.Select(e => e.Description)));

        // Make sure the account isn't locked out from prior bad attempts now that they've proven ownership.
        await _users.SetLockoutEndDateAsync(user, null);
        await _users.ResetAccessFailedCountAsync(user);
        _ = ct;
        return NoContent();
    }

    [HttpGet("me")]
    [Authorize]
    public async Task<ActionResult<MeResponse>> Me(CancellationToken ct)
    {
        var user = await _users.GetUserAsync(User);
        if (user is null) return Unauthorized();
        var account = await _db.ParentAccounts.FirstOrDefaultAsync(p => p.UserId == user.Id, ct);
        return Ok(await BuildMeAsync(user, account));
    }

    [HttpGet("external/{provider}")]
    public IActionResult ExternalLogin(string provider, [FromQuery] string? returnUrl)
    {
        var safe = SafeReturnUrl(returnUrl);
        var redirectUrl = Url.Action(nameof(ExternalCallback), "Auth", new { returnUrl = safe });
        var props = _signIn.ConfigureExternalAuthenticationProperties(provider, redirectUrl);
        return Challenge(props, provider);
    }

    [HttpGet("external/callback")]
    public async Task<IActionResult> ExternalCallback([FromQuery] string? returnUrl, CancellationToken ct)
    {
        var safe = SafeReturnUrl(returnUrl);
        var info = await _signIn.GetExternalLoginInfoAsync();
        if (info is null)
            return Redirect($"/login?error=external_failed");

        // Already linked? Sign in.
        var signin = await _signIn.ExternalLoginSignInAsync(info.LoginProvider, info.ProviderKey, isPersistent: true, bypassTwoFactor: true);
        if (signin.Succeeded)
        {
            var existingByLogin = await _users.FindByLoginAsync(info.LoginProvider, info.ProviderKey);
            if (existingByLogin is not null) await StampLastLoginAsync(existingByLogin);
            return Redirect(safe);
        }
        if (signin.IsLockedOut)
            return Redirect("/login?error=banned");

        // First-time external login: create user from claims and link.
        var email = info.Principal.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value;
        if (string.IsNullOrWhiteSpace(email))
            return Redirect("/login?error=no_email");

        var user = await _users.FindByEmailAsync(email);
        if (user is not null && await _users.IsLockedOutAsync(user))
            return Redirect("/login?error=banned");

        if (user is null)
        {
            user = new ApplicationUser
            {
                UserName = email,
                Email = email,
                EmailConfirmed = true
            };
            var create = await _users.CreateAsync(user);
            if (!create.Succeeded)
            {
                _logger.LogWarning("External user create failed: {Errors}", string.Join(",", create.Errors.Select(e => e.Description)));
                return Redirect("/login?error=create_failed");
            }

            var firstName = info.Principal.FindFirst(System.Security.Claims.ClaimTypes.GivenName)?.Value
                ?? info.Principal.Identity?.Name?.Split(' ').FirstOrDefault()
                ?? email.Split('@').FirstOrDefault()
                ?? "";
            var lastName = info.Principal.FindFirst(System.Security.Claims.ClaimTypes.Surname)?.Value
                ?? info.Principal.Identity?.Name?.Split(' ').Skip(1).LastOrDefault()
                ?? "";
            var account = new ParentAccount
            {
                UserId = user.Id,
                FirstName = firstName,
                LastName = lastName,
                Language = Language.English
            };
            _db.ParentAccounts.Add(account);
            await _db.SaveChangesAsync(ct);

            await AttributeOutreachAsync(email, null, account.Id, OutreachStatus.AccountCreated, ct);
        }

        var addLogin = await _users.AddLoginAsync(user, info);
        if (!addLogin.Succeeded)
        {
            _logger.LogWarning("AddLogin failed for {Email}: {Errors}", email, string.Join(",", addLogin.Errors.Select(e => e.Description)));
            return Redirect("/login?error=link_failed");
        }

        await _signIn.SignInAsync(user, isPersistent: true);
        await StampLastLoginAsync(user);
        return Redirect(safe);
    }

    private async Task<MeResponse> BuildMeAsync(ApplicationUser user, ParentAccount? account)
    {
        var roles = await _users.GetRolesAsync(user);
        var coachTeams = await _coaches.GetCoachTeamIdsAsync(user, HttpContext.RequestAborted);
        return new MeResponse(
            user.Id,
            user.Email ?? "",
            account?.FirstName ?? "",
            account?.LastName ?? "",
            account?.CellPhone,
            account?.Language ?? Language.English,
            roles.Contains(Roles.Admin),
            coachTeams.Count > 0
        );
    }

    private async Task AttributeOutreachAsync(string? email, string? phone, int parentAccountId, OutreachStatus status, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(email) && string.IsNullOrWhiteSpace(phone)) return;

        var query = _db.Outreaches.Where(o => o.ParentAccountId == null);
        if (!string.IsNullOrWhiteSpace(email))
            query = query.Where(o => o.Email == email);
        else
            query = query.Where(o => o.Phone == phone);

        var match = await query.OrderByDescending(o => o.CreatedAt).FirstOrDefaultAsync(ct);
        if (match is null) return;

        match.ParentAccountId = parentAccountId;
        if (match.Status < status) match.Status = status;
        if (status == OutreachStatus.AccountCreated) match.AccountCreatedAt = DateTime.UtcNow;
        if (status == OutreachStatus.Registered) match.RegisteredAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
    }

    private string SafeReturnUrl(string? returnUrl)
    {
        if (string.IsNullOrWhiteSpace(returnUrl)) return "/";
        // Only allow same-site relative paths.
        if (Uri.TryCreate(returnUrl, UriKind.Relative, out _) && returnUrl.StartsWith("/") && !returnUrl.StartsWith("//"))
            return returnUrl;
        return "/";
    }
}
