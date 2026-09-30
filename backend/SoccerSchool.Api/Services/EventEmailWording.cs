using System.Text;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using SoccerSchool.Api.Data;
using SoccerSchool.Api.Domain;

namespace SoccerSchool.Api.Services;

/// <summary>The editable text of one event email: subject, message above the details, footer under the buttons.</summary>
public record EventEmailWording(string Subject, string Message, string Footer)
{
    /// <summary>Built-in wording, used until an admin saves their own (and after "Reset to default").</summary>
    public static EventEmailWording Default(EventEmailKind kind, Language lang) => (kind, lang) switch
    {
        (EventEmailKind.Created, Language.Spanish) => new(
            "Nuevo en el calendario: {team.name} {event.title} — {event.whenShort}",
            "Hola {parent.name}:\n\nHay una nueva actividad en el calendario de {team.name}: {event.title}.",
            "Toque una opción para responder. Puede cambiarla en cualquier momento. También puede verlo en la app LV Soccer School."),
        (EventEmailKind.Updated, Language.Spanish) => new(
            "Actualizado: {team.name} {event.title} — {event.whenShort}",
            "Hola {parent.name}:\n\nHubo cambios en esta actividad de {team.name}: {event.changes}.",
            "Toque una opción para responder. Puede cambiarla en cualquier momento. También puede verlo en la app LV Soccer School."),
        (EventEmailKind.Updated, _) => new(
            "Updated: {team.name} {event.title} — {event.whenShort}",
            "Hi {parent.name},\n\nThis {team.name} {event.type} was updated: {event.changes}.",
            "Tap an option to answer. You can change it any time. You can also see it in the LV Soccer School app."),
        _ => new(
            "New {event.type}: {team.name} {event.title} — {event.whenShort}",
            "Hi {parent.name},\n\nA new {event.type} was added for {team.name}.",
            "Tap an option to answer. You can change it any time. You can also see it in the LV Soccer School app."),
    };

    /// <summary>The saved wording for every kind + language, falling back to the defaults.</summary>
    public static async Task<Func<EventEmailKind, Language, EventEmailWording>> LoadAllAsync(AppDbContext db, CancellationToken ct)
    {
        var saved = await db.EventEmailTemplates.AsNoTracking().ToListAsync(ct);
        var map = saved.ToDictionary(t => (t.Kind, t.Language), t => new EventEmailWording(t.Subject, t.Message, t.Footer));
        return (kind, lang) => map.TryGetValue((kind, lang), out var w) ? w : Default(kind, lang);
    }
}

public record EventEmailPlaceholder(string Key, string LabelEn, string LabelEs);

/// <summary>Placeholders the event-email text can use, and the substitution itself.</summary>
public static class EventEmailPlaceholders
{
    public static readonly IReadOnlyList<EventEmailPlaceholder> All = new EventEmailPlaceholder[]
    {
        new("parent.name", "Parent's first name", "Nombre del padre/madre"),
        new("players.names", "Player first names (this family)", "Nombres de los jugadores (esta familia)"),
        new("team.name", "Team name", "Nombre del equipo"),
        new("event.type", "Game / practice / event", "Partido / práctica / evento"),
        new("event.title", "Title (\"vs Rebels\" or the practice title)", "Título (\"vs Rebels\" o el título de la práctica)"),
        new("event.when", "Date, start and end time", "Fecha, hora de inicio y fin"),
        new("event.whenShort", "Short date and time", "Fecha y hora cortas"),
        new("event.date", "Date", "Fecha"),
        new("event.time", "Start time", "Hora de inicio"),
        new("event.arrive", "Arrive-by time", "Hora de llegada"),
        new("event.place", "Location", "Lugar"),
        new("event.opponent", "Opponent", "Oponente"),
        new("event.notes", "Notes", "Notas"),
        new("event.changes", "What changed (update emails)", "Qué cambió (correos de cambios)"),
    };

    private static readonly Regex Token = new(@"\{([a-zA-Z]+\.[a-zA-Z]+)\}", RegexOptions.Compiled);
    // "Hi {parent.name}," with no name on file → "Hi,", not "Hi ,".
    private static readonly Regex SpaceBeforePunct = new(@"[ \t]+([,:.])", RegexOptions.Compiled);

    public static IReadOnlyDictionary<string, string> Values(
        EventMessageText t, EventSnapshot e, IReadOnlyList<EventField> changes, string recipientName, IEnumerable<string> kidNames) =>
        new Dictionary<string, string>
        {
            ["parent.name"] = recipientName.Trim(),
            ["players.names"] = string.Join(", ", kidNames),
            ["team.name"] = e.TeamName,
            ["event.type"] = t.KindLower(e.Kind),
            ["event.title"] = t.Title(e),
            ["event.when"] = t.WhenLong(e),
            ["event.whenShort"] = t.WhenShort(e.StartsAt),
            ["event.date"] = t.Date(e.StartsAt),
            ["event.time"] = t.Time(e.StartsAt),
            ["event.arrive"] = e.ArriveAt is DateTime a ? t.Time(a) : string.Empty,
            ["event.place"] = t.Place(e),
            ["event.opponent"] = e.OpponentName ?? string.Empty,
            ["event.notes"] = e.Notes ?? string.Empty,
            ["event.changes"] = string.Join(", ", changes.Select(t.FieldLabel)),
        };

    /// <summary>Replaces known placeholders; unknown ones are left as typed so a typo is visible in the preview.</summary>
    public static string Render(string text, IReadOnlyDictionary<string, string> values)
    {
        if (string.IsNullOrEmpty(text)) return string.Empty;
        var result = Token.Replace(text, m => values.TryGetValue(m.Groups[1].Value, out var v) ? v : m.Value);
        return SpaceBeforePunct.Replace(result, "$1");
    }

    private static readonly Regex Url = new(@"https?://[^\s<]+", RegexOptions.Compiled);

    /// <summary>Plain text → HTML paragraphs: blank line = new paragraph, line break kept, links clickable.</summary>
    public static string ToHtmlParagraphs(string text, string paragraphStyle)
    {
        var html = new StringBuilder();
        foreach (var para in Regex.Split(text.Replace("\r\n", "\n").Trim(), @"\n\s*\n"))
        {
            if (string.IsNullOrWhiteSpace(para)) continue;
            var body = Url.Replace(EventEmail.Esc(para.Trim()), m =>
            {
                // A sentence-ending period isn't part of the link.
                var url = m.Value.TrimEnd('.', ',', ')');
                return $"<a href=\"{url}\" style=\"color:#0b6b4f\">{url}</a>{m.Value[url.Length..]}";
            });
            html.Append($"<p style=\"{paragraphStyle}\">{body.Replace("\n", "<br/>")}</p>");
        }
        return html.ToString();
    }
}
