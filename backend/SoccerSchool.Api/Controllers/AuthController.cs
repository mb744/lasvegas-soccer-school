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

namespace SoccerSchool.Api.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly UserManager<ApplicationUser> _users;
    private readonly SignInManager<ApplicationUser> _signIn;
    private readonly AppDbContext _db;
    private readonly AppOptions _app;
    private readonly ILogger<AuthController> _logger;

    public AuthController(
        UserManager<ApplicationUser> users,
        SignInManager<ApplicationUser> signIn,
        AppDbContext db,
        IOptions<AppOptions> app,
        ILogger<AuthController> logger)
    {
        _users = users;
        _signIn = signIn;
        _db = db;
        _app = app.Value;
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

        var account = new ParentAccount
        {
            UserId = user.Id,
            FirstName = req.FirstName.Trim(),
            LastName = req.LastName.Trim(),
            CellPhone = req.Phone?.Trim(),
            Language = req.Language
        };
        _db.ParentAccounts.Add(account);
        await _db.SaveChangesAsync(ct);

        await AttributeOutreachAsync(req.Email, req.Phone, account.Id, OutreachStatus.AccountCreated, ct);

        await _signIn.SignInAsync(user, isPersistent: true);
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

        var account = await _db.ParentAccounts.FirstOrDefaultAsync(p => p.UserId == user.Id, ct);
        return Ok(await BuildMeAsync(user, account));
    }

    [HttpPost("logout")]
    public async Task<IActionResult> Logout()
    {
        await _signIn.SignOutAsync();
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
            return Redirect(safe);

        // First-time external login: create user from claims and link.
        var email = info.Principal.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value;
        if (string.IsNullOrWhiteSpace(email))
            return Redirect("/login?error=no_email");

        var user = await _users.FindByEmailAsync(email);
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
        return Redirect(safe);
    }

    private async Task<MeResponse> BuildMeAsync(ApplicationUser user, ParentAccount? account)
    {
        var roles = await _users.GetRolesAsync(user);
        return new MeResponse(
            user.Id,
            user.Email ?? "",
            account?.FirstName ?? "",
            account?.LastName ?? "",
            account?.CellPhone,
            account?.Language ?? Language.English,
            roles.Contains(Roles.Admin)
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
