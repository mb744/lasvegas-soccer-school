using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SoccerSchool.Api.Data;
using SoccerSchool.Api.Domain;
using SoccerSchool.Api.Options;

namespace SoccerSchool.Api.Services;

public record PreferencesFooter(string Plain, string Html);

/// <summary>
/// The "Update your preferences here" line every LVSS email ends with. Worked out from the
/// recipient address: a login, or a family contact without one. Addresses we don't know
/// (outside invitees, "send to anyone") have no settings to change, so they get no link.
/// </summary>
public interface IEmailPreferencesLink
{
    Task<PreferencesFooter?> ForAsync(string toEmail, CancellationToken ct);
}

public class EmailPreferencesLink : IEmailPreferencesLink
{
    /// <summary>Emails that already carry the link (event emails put it in their own layout).</summary>
    public const string PathMarker = "/notification-preferences?t=";

    private readonly IServiceScopeFactory _scopes;
    private readonly IPreferenceTokens _tokens;
    private readonly AppOptions _app;

    public EmailPreferencesLink(IServiceScopeFactory scopes, IPreferenceTokens tokens, IOptions<AppOptions> app)
    {
        _scopes = scopes;
        _tokens = tokens;
        _app = app.Value;
    }

    public async Task<PreferencesFooter?> ForAsync(string toEmail, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(toEmail)) return null;
        var email = toEmail.Trim();

        // Own scope: callers may be mid-way through their own DbContext work.
        using var scope = _scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        PreferenceSubject? subject = null;
        var language = Language.English;
        var normalized = email.ToUpperInvariant();
        var user = await db.Users.AsNoTracking()
            .Where(u => u.NormalizedEmail == normalized)
            .Select(u => new { u.Id, Lang = u.ParentAccount != null ? u.ParentAccount.Language : Language.English })
            .FirstOrDefaultAsync(ct);
        if (user is not null)
        {
            subject = PreferenceSubject.ForUser(user.Id);
            language = user.Lang;
        }
        else
        {
            var lower = email.ToLowerInvariant();
            var contact = await db.ParentContacts.AsNoTracking()
                .Where(c => c.Email != null && c.Email.ToLower() == lower)
                .OrderByDescending(c => c.UserId != null)
                .Select(c => new { c.Id, c.UserId, c.Language })
                .FirstOrDefaultAsync(ct);
            if (contact is not null)
            {
                subject = contact.UserId is not null ? PreferenceSubject.ForUser(contact.UserId) : PreferenceSubject.ForContact(contact.Id);
                language = contact.Language;
            }
        }
        if (subject is null) return null;

        var url = $"{(_app.PublicBaseUrl ?? string.Empty).TrimEnd('/')}{PathMarker}{Uri.EscapeDataString(_tokens.Create(subject))}";
        var es = language == Language.Spanish;
        var question = es ? "¿No quiere recibir estos correos?" : "Don’t want these emails?";
        var link = es ? "Actualice sus preferencias aquí." : "Update your preferences here.";
        return new PreferencesFooter(
            $"{question} {link}: {url}",
            "<p style=\"font-family:Arial,Helvetica,sans-serif;font-size:12px;color:#5b6b63;text-align:center;margin:20px 0 0\">"
            + $"{EventEmail.Esc(question)} <a href=\"{EventEmail.Esc(url)}\" style=\"color:#0b6b4f\">{EventEmail.Esc(link)}</a></p>");
    }

    /// <summary>Adds the footer to both bodies (HTML just before &lt;/body&gt;), unless the email already has the link.</summary>
    public static (string Plain, string Html) AddTo(string plain, string html, PreferencesFooter? footer)
    {
        if (footer is null || html.Contains(PathMarker, StringComparison.Ordinal)) return (plain, html);
        var at = html.LastIndexOf("</body>", StringComparison.OrdinalIgnoreCase);
        var newHtml = at >= 0 ? html.Insert(at, footer.Html) : html + footer.Html;
        return ($"{plain.TrimEnd()}\n\n{footer.Plain}\n", newHtml);
    }
}
