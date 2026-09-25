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
/// Sign-in for kids in the Daily Training app: username/password → PlayerJwt access token +
/// rotating refresh token. Also the kid-side "Forgot password?" which emails the PARENT a reset
/// link (kids have no email and never reset their own password).
/// </summary>
[ApiController]
[Route("api/mobile/player-auth")]
[AllowAnonymous]
public class PlayerAuthController : ControllerBase
{
    private const string InvalidCredentials = "Invalid username or password.";

    private readonly AppDbContext _db;
    private readonly IPlayerTokenService _tokens;
    private readonly IEmailSender _email;
    private readonly IPasswordHasher<PlayerLogin> _hasher;
    private readonly AppOptions _app;
    private readonly ILogger<PlayerAuthController> _logger;

    public PlayerAuthController(
        AppDbContext db,
        IPlayerTokenService tokens,
        IEmailSender email,
        IPasswordHasher<PlayerLogin> hasher,
        IOptions<AppOptions> app,
        ILogger<PlayerAuthController> logger)
    {
        _db = db;
        _tokens = tokens;
        _email = email;
        _hasher = hasher;
        _app = app.Value;
        _logger = logger;
    }

    [HttpPost("login")]
    public async Task<ActionResult<PlayerTokenResponse>> Login([FromBody] PlayerLoginRequest req, CancellationToken ct)
    {
        if (!_app.Jwt.IsConfigured) return StatusCode(503, "Sign-in is not configured.");

        var username = PlayerCredentials.NormalizeUsername(req.Username);
        var login = await _db.PlayerLogins.Include(l => l.Player).FirstOrDefaultAsync(l => l.Username == username, ct);
        if (login is null)
        {
            // Burn comparable time so response latency doesn't reveal which usernames exist.
            _hasher.VerifyHashedPassword(new PlayerLogin(), DummyHash, req.Password);
            return Unauthorized(InvalidCredentials);
        }

        var now = DateTime.UtcNow;
        if (login.LockoutEnd > now)
            return StatusCode(StatusCodes.Status429TooManyRequests, "Too many tries. Wait a few minutes and try again.");

        var result = _hasher.VerifyHashedPassword(login, login.PasswordHash, req.Password);
        if (result == PasswordVerificationResult.Failed)
        {
            login.AccessFailedCount++;
            if (login.AccessFailedCount >= PlayerCredentials.MaxFailedAttempts)
            {
                login.LockoutEnd = now.Add(PlayerCredentials.LockoutDuration);
                login.AccessFailedCount = 0;
            }
            await _db.SaveChangesAsync(ct);
            return Unauthorized(InvalidCredentials);
        }

        if (result == PasswordVerificationResult.SuccessRehashNeeded)
            login.PasswordHash = _hasher.HashPassword(login, req.Password);
        login.AccessFailedCount = 0;
        login.LockoutEnd = null;
        login.LastLoginAt = now;
        await _db.SaveChangesAsync(ct);

        var pair = await _tokens.IssueAsync(login, ct);
        return Ok(await BuildResponseAsync(login, pair, ct));
    }

    [HttpPost("refresh")]
    public async Task<ActionResult<PlayerTokenResponse>> Refresh([FromBody] MobileRefreshRequest req, CancellationToken ct)
    {
        var refreshed = await _tokens.RefreshAsync(req.RefreshToken, ct);
        if (refreshed is null) return Unauthorized();
        var (pair, login) = refreshed.Value;
        await _db.Entry(login).Reference(l => l.Player).LoadAsync(ct);
        return Ok(await BuildResponseAsync(login, pair, ct));
    }

    [HttpPost("logout")]
    public async Task<IActionResult> Logout([FromBody] MobileRefreshRequest req, CancellationToken ct)
    {
        await _tokens.RevokeAsync(req.RefreshToken, ct);
        return NoContent();
    }

    /// <summary>Emails the kid's parent a link to set a new password. Always 204 — whether or not the
    /// username exists — so the endpoint can't be used to discover usernames.</summary>
    [HttpPost("forgot-password")]
    public async Task<IActionResult> ForgotPassword([FromBody] PlayerForgotPasswordRequest req, CancellationToken ct)
    {
        var username = PlayerCredentials.NormalizeUsername(req.Username);
        var login = await _db.PlayerLogins
            .Include(l => l.Player!).ThenInclude(p => p.ParentAccount!).ThenInclude(a => a.User)
            .FirstOrDefaultAsync(l => l.Username == username, ct);
        var account = login?.Player?.ParentAccount;
        var parentEmail = account?.User?.Email;
        if (login is null || account is null || string.IsNullOrWhiteSpace(parentEmail))
            return NoContent();

        // Cooldown: repeated taps don't flood the parent's inbox.
        var now = DateTime.UtcNow;
        var recent = await _db.PlayerPasswordResetTokens.AnyAsync(t =>
            t.PlayerLoginId == login.Id && t.CreatedAt > now - PlayerCredentials.ResetEmailCooldown, ct);
        if (recent) return NoContent();

        var rawToken = PlayerCredentials.GenerateToken();
        _db.PlayerPasswordResetTokens.Add(new PlayerPasswordResetToken
        {
            PlayerLoginId = login.Id,
            TokenHash = PlayerCredentials.HashToken(rawToken),
            CreatedAt = now,
            ExpiresAt = now.Add(PlayerCredentials.ResetTokenLifetime),
        });
        await _db.SaveChangesAsync(ct);

        var baseUrl = (_app.PublicBaseUrl ?? string.Empty).TrimEnd('/');
        // ?lang= makes the web page open in the parent's language (see frontend i18n detection).
        var lang = account.Language == Language.Spanish ? "es" : "en";
        var link = $"{baseUrl}/reset-player-password?token={Uri.EscapeDataString(rawToken)}&lang={lang}";
        var kid = login.Player!.FirstName.Trim();
        var parent = string.IsNullOrWhiteSpace(account.FirstName) ? null : account.FirstName.Trim();

        string subject, body;
        if (account.Language == Language.Spanish)
        {
            subject = $"Restablecer la contraseña de {kid} — Entrenamiento Diario LVSS";
            body =
                $"Hola{(parent is null ? "" : " " + parent)},\n\n" +
                $"{kid} pidió ayuda con su contraseña en la app Entrenamiento Diario de Las Vegas Soccer School (usuario: {login.Username}).\n\n" +
                $"Abra este enlace para elegir una nueva contraseña. El enlace vence en 24 horas:\n{link}\n\n" +
                "También puede cambiarla en cualquier momento desde la app LV Soccer School, en su perfil.\n\n" +
                "Si nadie en su familia lo pidió, puede ignorar este correo.\n\n" +
                "Gracias,\nLas Vegas Soccer School";
        }
        else
        {
            subject = $"Reset {kid}'s password — LVSS Daily Training";
            body =
                $"Hi{(parent is null ? "" : " " + parent)},\n\n" +
                $"{kid} asked for help with their password in the Las Vegas Soccer School Daily Training app (username: {login.Username}).\n\n" +
                $"Open this link to choose a new password. The link expires in 24 hours:\n{link}\n\n" +
                "You can also change it any time from your profile in the LV Soccer School app.\n\n" +
                "If nobody in your family asked for this, you can ignore this email.\n\n" +
                "Thanks,\nLas Vegas Soccer School";
        }

        var send = await _email.SendAsync(parentEmail!, subject, body, ct);
        if (!send.Success)
            _logger.LogWarning("Player password reset email to parent account {AccountId} failed: {Message}", account.Id, send.Message);
        return NoContent();
    }

    private async Task<PlayerTokenResponse> BuildResponseAsync(PlayerLogin login, TokenPair pair, CancellationToken ct)
    {
        var me = await TrainingController.BuildMeAsync(_db, login, ct);
        return new PlayerTokenResponse(pair.AccessToken, pair.AccessTokenExpiresAt, pair.RefreshToken, pair.RefreshTokenExpiresAt, me);
    }

    // A valid hash of a random throwaway password, verified against when the username doesn't exist.
    private static readonly string DummyHash =
        new PasswordHasher<PlayerLogin>().HashPassword(new PlayerLogin(), Guid.NewGuid().ToString("N"));
}
