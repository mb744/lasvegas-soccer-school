using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SoccerSchool.Api.Data;
using SoccerSchool.Api.Domain;
using SoccerSchool.Api.Options;

namespace SoccerSchool.Api.Services;

/// <summary>
/// Owns <see cref="IdentityUser.EmailConfirmed"/>. Sign-up doesn't require it, but anything that
/// grants access because an email *matches* (coach cards, additional-parent contacts, team-chat
/// coach seeding) only trusts verified logins — otherwise registering someone else's address first
/// would hand over their team or family. A login becomes verified by:
/// <list type="bullet">
/// <item>opening the confirmation link this service emails (<c>/confirm-email</c>);</item>
/// <item>completing a password reset by email (same proof of inbox access);</item>
/// <item>signing in with Google/Facebook, whose email the provider has verified.</item>
/// </list>
/// </summary>
public interface IEmailVerificationService
{
    /// <summary>Emails a confirmation link. No-op for already-verified logins. Never throws; returns
    /// false when the email couldn't be sent.</summary>
    Task<bool> SendConfirmationAsync(ApplicationUser user, CancellationToken ct);

    /// <summary>Validates a link from <see cref="SendConfirmationAsync"/>. True when the login is
    /// now verified (including when it already was).</summary>
    Task<bool> ConfirmAsync(string userId, string encodedToken, CancellationToken ct);

    /// <summary>Marks the login verified after it proved inbox access another way (password reset).</summary>
    Task MarkVerifiedAsync(ApplicationUser user, CancellationToken ct);

    /// <summary>Called when a Google/Facebook sign-in lands on an existing login by email. If that
    /// login was never verified, whoever created it never proved they own the address — so its
    /// password is removed and every session revoked before marking it verified. The real owner
    /// (who just proved ownership via the provider) keeps the account; they can set a password
    /// again with "Forgot password".</summary>
    Task AdoptProviderVerifiedEmailAsync(ApplicationUser user, CancellationToken ct);
}

public class EmailVerificationService : IEmailVerificationService
{
    private readonly UserManager<ApplicationUser> _users;
    private readonly AppDbContext _db;
    private readonly IEmailSender _email;
    private readonly AppOptions _app;
    private readonly IHostEnvironment _env;
    private readonly ILogger<EmailVerificationService> _logger;

    public EmailVerificationService(
        UserManager<ApplicationUser> users,
        AppDbContext db,
        IEmailSender email,
        IOptions<AppOptions> app,
        IHostEnvironment env,
        ILogger<EmailVerificationService> logger)
    {
        _users = users;
        _db = db;
        _email = email;
        _app = app.Value;
        _env = env;
        _logger = logger;
    }

    public async Task<bool> SendConfirmationAsync(ApplicationUser user, CancellationToken ct)
    {
        if (user.EmailConfirmed || string.IsNullOrWhiteSpace(user.Email)) return true;

        // Identity tokens are standard Base64 (+, /, =); Base64Url keeps them intact in a query string.
        var token = await _users.GenerateEmailConfirmationTokenAsync(user);
        var encoded = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(token));
        var language = await _db.ParentAccounts
            .Where(a => a.UserId == user.Id)
            .Select(a => (Language?)a.Language)
            .FirstOrDefaultAsync(ct) ?? Language.English;
        var baseUrl = (_app.PublicBaseUrl ?? string.Empty).TrimEnd('/');
        var link = $"{baseUrl}/confirm-email?userId={Uri.EscapeDataString(user.Id)}&token={encoded}" +
                   $"&lang={(language == Language.Spanish ? "es" : "en")}";

        string subject, body;
        if (language == Language.Spanish)
        {
            subject = "Confirme su correo — Las Vegas Soccer School";
            body =
                "Hola,\n\n" +
                "Confirme que este es su correo para su cuenta de Las Vegas Soccer School:\n\n" +
                $"{link}\n\n" +
                "Confirmarlo conecta su cuenta con los equipos que entrena y las familias que comparte.\n\n" +
                "Si usted no creó esta cuenta, puede ignorar este correo.\n\n" +
                "Gracias,\nLas Vegas Soccer School";
        }
        else
        {
            subject = "Confirm your email — Las Vegas Soccer School";
            body =
                "Hi,\n\n" +
                "Please confirm this is your email address for your Las Vegas Soccer School account:\n\n" +
                $"{link}\n\n" +
                "Confirming connects your account to the teams you coach and the families you share.\n\n" +
                "If you didn't create this account, you can ignore this email.\n\n" +
                "Thanks,\nLas Vegas Soccer School";
        }

        // Local development has no email service; log the link so the flow can still be completed.
        // Never in production — the link is a credential for the address.
        if (!_email.IsAvailable && _env.IsDevelopment())
        {
            _logger.LogInformation("DEV ONLY — email confirmation link for {Email}: {Link}", user.Email, link);
            return true;
        }

        var send = await _email.SendAsync(user.Email, subject, body, ct);
        if (!send.Success)
            _logger.LogWarning("Email confirmation send failed for user {UserId}: {Message}", user.Id, send.Message);
        return send.Success;
    }

    public async Task<bool> ConfirmAsync(string userId, string encodedToken, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(userId) || string.IsNullOrWhiteSpace(encodedToken)) return false;
        var user = await _users.FindByIdAsync(userId);
        if (user is null) return false;
        if (user.EmailConfirmed) return true;

        string token;
        try
        {
            token = Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(encodedToken));
        }
        catch (FormatException)
        {
            return false;
        }
        var result = await _users.ConfirmEmailAsync(user, token);
        return result.Succeeded;
    }

    public async Task MarkVerifiedAsync(ApplicationUser user, CancellationToken ct)
    {
        if (user.EmailConfirmed) return;
        user.EmailConfirmed = true;
        await _users.UpdateAsync(user);
    }

    public async Task AdoptProviderVerifiedEmailAsync(ApplicationUser user, CancellationToken ct)
    {
        if (user.EmailConfirmed) return;

        if (await _users.HasPasswordAsync(user))
        {
            await _users.RemovePasswordAsync(user);
            _logger.LogWarning(
                "Removed unverified password from user {UserId}: a provider-verified sign-in claimed the same email.",
                user.Id);
        }

        // Cut off anyone already signed in with the unverified credentials: new security stamp
        // (invalidates web cookies) and revoke every mobile refresh token.
        await _users.UpdateSecurityStampAsync(user);
        var now = DateTime.UtcNow;
        var refreshTokens = await _db.MobileRefreshTokens
            .Where(t => t.UserId == user.Id && t.RevokedAt == null)
            .ToListAsync(ct);
        foreach (var t in refreshTokens) t.RevokedAt = now;
        await _db.SaveChangesAsync(ct);

        user.EmailConfirmed = true;
        await _users.UpdateAsync(user);
    }
}
