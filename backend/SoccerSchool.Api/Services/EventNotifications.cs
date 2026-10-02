using System.Globalization;
using System.Text;
using System.Threading.Channels;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SoccerSchool.Api.Data;
using SoccerSchool.Api.Domain;
using SoccerSchool.Api.Options;

namespace SoccerSchool.Api.Services;

// ---------------------------------------------------------------------------------------------
// Snapshot + diff: what parents see about an event, captured before an edit and compared after.
// ---------------------------------------------------------------------------------------------

public enum EventField { When, Arrive, Place, Opponent, HomeAway, Uniform, Shoes, Title, Notes }

public record EventSnapshot(
    ScheduledEventKind Kind,
    string TeamName,
    DateTime StartsAt,
    DateTime? EndsAt,
    DateTime? ArriveAt,
    string? Location,
    string? VenueName,
    string? VenueAddress,
    string? OpponentName,
    bool? IsHome,
    string? Uniform,
    ShoeType ShoeType,
    string? Summary,
    string? Notes,
    bool IsCancelled)
{
    public static async Task<EventSnapshot?> LoadAsync(AppDbContext db, int eventId, CancellationToken ct)
    {
        var e = await db.ScheduledGames.AsNoTracking()
            .Where(g => g.Id == eventId)
            .Select(g => new
            {
                g.Kind, TeamName = g.Team!.Name, g.StartsAt, g.EndsAt, g.ArriveAt, g.Location,
                VenueName = g.Venue != null ? g.Venue.Name : null,
                VenueAddress = g.Venue != null ? g.Venue.Address : null,
                g.OpponentName, g.IsHome, g.Uniform, g.ShoeType, g.Summary, g.Description, g.IsCancelled,
            })
            .FirstOrDefaultAsync(ct);
        if (e is null) return null;

        // Same rule the app uses: the event's own uniform, else the club default for home/away/practice.
        var uniform = e.Uniform;
        if (uniform is null)
        {
            var designation = e.Kind == ScheduledEventKind.Practice ? UniformDesignation.Practice
                : e.IsHome switch { true => UniformDesignation.Home, false => UniformDesignation.Away, _ => UniformDesignation.None };
            if (designation != UniformDesignation.None)
                uniform = await db.Uniforms.AsNoTracking().FirstOrDefaultAsync(u => u.Designation == designation, ct);
        }

        return new EventSnapshot(e.Kind, e.TeamName, e.StartsAt, e.EndsAt, e.ArriveAt, e.Location, e.VenueName,
            e.VenueAddress, e.OpponentName, e.IsHome, uniform?.ToWearText(), e.ShoeType, e.Summary, e.Description, e.IsCancelled);
    }

    /// <summary>Parent-facing fields that differ. Empty = nothing worth telling parents about.</summary>
    public static IReadOnlyList<EventField> Diff(EventSnapshot before, EventSnapshot after)
    {
        var changes = new List<EventField>();
        if (before.StartsAt != after.StartsAt || before.EndsAt != after.EndsAt) changes.Add(EventField.When);
        if (before.ArriveAt != after.ArriveAt) changes.Add(EventField.Arrive);
        if (Norm(before.VenueName) != Norm(after.VenueName) || Norm(before.Location) != Norm(after.Location)
            || Norm(before.VenueAddress) != Norm(after.VenueAddress)) changes.Add(EventField.Place);
        if (Norm(before.OpponentName) != Norm(after.OpponentName)) changes.Add(EventField.Opponent);
        if (before.IsHome != after.IsHome) changes.Add(EventField.HomeAway);
        if (Norm(before.Uniform) != Norm(after.Uniform)) changes.Add(EventField.Uniform);
        if (before.ShoeType != after.ShoeType) changes.Add(EventField.Shoes);
        // A game's title follows its opponent, so only practices/events get a separate title change.
        if (after.Kind != ScheduledEventKind.Game && Norm(before.Summary) != Norm(after.Summary)) changes.Add(EventField.Title);
        if (Norm(before.Notes) != Norm(after.Notes)) changes.Add(EventField.Notes);
        return changes;
    }

    private static string Norm(string? s) => (s ?? string.Empty).Trim();
}

// ---------------------------------------------------------------------------------------------
// Signed one-click attendance links ("Going / Maybe / Not going" in the email).
// ---------------------------------------------------------------------------------------------

public interface IRsvpTokens
{
    string Create(int eventId, int playerId, DateTime eventStartsAtUtc);
    bool TryRead(string token, out int eventId, out int playerId);
}

public class RsvpTokens : IRsvpTokens
{
    private readonly ITimeLimitedDataProtector _protector;

    public RsvpTokens(IDataProtectionProvider provider) =>
        _protector = provider.CreateProtector("LVSS.EmailRsvp.v1").ToTimeLimitedDataProtector();

    // Valid until a day after the event starts (at least a day from now), so late answers still work.
    public string Create(int eventId, int playerId, DateTime eventStartsAtUtc)
    {
        var expires = eventStartsAtUtc.AddDays(1);
        if (expires < DateTime.UtcNow.AddDays(1)) expires = DateTime.UtcNow.AddDays(1);
        return _protector.Protect($"{eventId}:{playerId}", new DateTimeOffset(DateTime.SpecifyKind(expires, DateTimeKind.Utc)));
    }

    public bool TryRead(string token, out int eventId, out int playerId)
    {
        eventId = playerId = 0;
        if (string.IsNullOrWhiteSpace(token)) return false;
        try
        {
            var parts = _protector.Unprotect(token).Split(':');
            return parts.Length == 2 && int.TryParse(parts[0], out eventId) && int.TryParse(parts[1], out playerId);
        }
        catch (System.Security.Cryptography.CryptographicException)
        {
            return false; // tampered or expired
        }
    }
}

// ---------------------------------------------------------------------------------------------
// Queue + worker: controllers enqueue after saving, so a save never waits on email/push.
// ---------------------------------------------------------------------------------------------

/// <summary>Before is null for a new event; for an edit it's the snapshot taken before saving.</summary>
public record EventNotificationJob(int EventId, EventSnapshot? Before);

public class EventNotificationQueue
{
    private readonly Channel<EventNotificationJob> _channel = Channel.CreateUnbounded<EventNotificationJob>();
    public void Enqueue(EventNotificationJob job) => _channel.Writer.TryWrite(job);
    public ChannelReader<EventNotificationJob> Reader => _channel.Reader;
}

public class EventNotificationWorker : BackgroundService
{
    private readonly EventNotificationQueue _queue;
    private readonly IServiceScopeFactory _scopes;
    private readonly ILogger<EventNotificationWorker> _logger;

    public EventNotificationWorker(EventNotificationQueue queue, IServiceScopeFactory scopes, ILogger<EventNotificationWorker> logger)
    {
        _queue = queue;
        _scopes = scopes;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var job in _queue.Reader.ReadAllAsync(stoppingToken))
        {
            try
            {
                using var scope = _scopes.CreateScope();
                await scope.ServiceProvider.GetRequiredService<EventNotificationSender>().SendAsync(job, stoppingToken);
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            {
                _logger.LogError(ex, "Event notification for event {EventId} failed.", job.EventId);
            }
        }
    }
}

// ---------------------------------------------------------------------------------------------
// Sender: push to each family's logins, email each parent/guardian with per-kid answer buttons.
// ---------------------------------------------------------------------------------------------

public record EventNotificationResult(int Pushes, int Emails, IReadOnlyList<EventField> Changes);

public class EventNotificationSender
{
    private readonly AppDbContext _db;
    private readonly IPushSender _push;
    private readonly IEmailSender _email;
    private readonly IRsvpTokens _tokens;
    private readonly IPreferenceTokens _prefTokens;
    private readonly AppOptions _app;
    private readonly ILogger<EventNotificationSender> _logger;

    public EventNotificationSender(AppDbContext db, IPushSender push, IEmailSender email, IRsvpTokens tokens,
        IPreferenceTokens prefTokens, IOptions<AppOptions> app, ILogger<EventNotificationSender> logger)
    {
        _db = db;
        _push = push;
        _email = email;
        _tokens = tokens;
        _prefTokens = prefTokens;
        _app = app.Value;
        _logger = logger;
    }

    /// <summary>One rostered kid with what we need to reach their family.</summary>
    private record Kid(int PlayerId, string FirstName, int ParentAccountId, string? OwnerUserId, string? OwnerEmail,
        string OwnerName, Language OwnerLanguage, bool NoComms, EmailPreference OwnerGames, EmailPreference OwnerEvents);

    /// <summary>One email address, with the kids it answers for and whose preferences link it gets.</summary>
    private sealed record Recipient(string Name, Language Lang, PreferenceSubject? Subject, List<(int Id, string First)> Kids);

    public async Task<EventNotificationResult> SendAsync(EventNotificationJob job, CancellationToken ct)
    {
        var none = new EventNotificationResult(0, 0, Array.Empty<EventField>());
        var after = await EventSnapshot.LoadAsync(_db, job.EventId, ct);
        if (after is null || after.IsCancelled || after.StartsAt < DateTime.UtcNow) return none;

        var changes = job.Before is null ? Array.Empty<EventField>() : EventSnapshot.Diff(job.Before, after);
        if (job.Before is not null && changes.Count == 0) return none; // edit with nothing parent-facing

        var kids = await LoadKidsAsync(job.EventId, ct);
        if (kids.Count == 0) return none;

        // ---- Push: one per family, in the family's language, to owner + guardian logins ----
        var familyIds = kids.Select(k => k.ParentAccountId).Distinct().ToList();
        var guardianLogins = (await _db.ParentAccountCollaborators
                .Where(c => familyIds.Contains(c.ParentAccountId) && c.AccessLevel == FamilyAccessLevel.Guardian)
                .Select(c => new { c.ParentAccountId, c.UserId })
                .ToListAsync(ct))
            .GroupBy(c => c.ParentAccountId)
            .ToDictionary(g => g.Key, g => g.Select(x => x.UserId).ToList());
        var pushes = 0;
        foreach (var family in kids.GroupBy(k => k.ParentAccountId))
        {
            var first = family.First();
            var userIds = new List<string>();
            if (!string.IsNullOrEmpty(first.OwnerUserId)) userIds.Add(first.OwnerUserId);
            if (guardianLogins.TryGetValue(family.Key, out var more)) userIds.AddRange(more.Where(u => !string.IsNullOrEmpty(u)));
            if (userIds.Count == 0) continue;

            var text = EventMessageText.For(first.OwnerLanguage);
            var names = string.Join(", ", family.Select(k => k.FirstName));
            await _push.SendToUsersAsync(userIds.Distinct(), new PushNotification(
                job.Before is null ? text.PushNewTitle(after) : text.PushUpdatedTitle(after),
                job.Before is null ? text.PushNewBody(after, names) : text.PushUpdatedBody(after, changes),
                new Dictionary<string, object> { ["type"] = "event", ["eventId"] = job.EventId }), ct);
            pushes++;
        }

        var kind = job.Before is null ? EventEmailKind.Created : EventEmailKind.Updated;
        var emails = await EmailAsync(job.EventId, after, job.Before, changes, kind, kids, ct);

        _logger.LogInformation("Event {EventId} {Kind}: {Pushes} push(es), {Emails} email(s).",
            job.EventId, job.Before is null ? "created" : "updated", pushes, emails);
        return new EventNotificationResult(pushes, emails, changes);
    }

    /// <summary>The reminder email before an event, once per family: kids already reminded are
    /// skipped, and the rest are stamped before anything is sent. Returns the emails sent.</summary>
    public async Task<int> SendReminderAsync(int eventId, CancellationToken ct)
    {
        var e = await EventSnapshot.LoadAsync(_db, eventId, ct);
        if (e is null || e.IsCancelled || e.StartsAt < DateTime.UtcNow) return 0;

        var kids = await LoadKidsAsync(eventId, ct);
        var kidIds = kids.Select(k => k.PlayerId).ToList();
        var rows = await _db.EventAttendances
            .Where(a => a.ScheduledGameId == eventId && kidIds.Contains(a.PlayerId))
            .ToDictionaryAsync(a => a.PlayerId, ct);
        // Only kids still without an answer: a family that already said Going / Maybe / Not going
        // isn't asked again.
        var due = kids.Where(k => !rows.TryGetValue(k.PlayerId, out var r)
            || (r.ReminderEmailSentAt is null && r.Status == AttendanceStatus.Pending)).ToList();
        if (due.Count == 0) return 0;

        var now = DateTime.UtcNow;
        foreach (var k in due)
        {
            if (!rows.TryGetValue(k.PlayerId, out var row))
            {
                row = new EventAttendance { ScheduledGameId = eventId, PlayerId = k.PlayerId, Status = AttendanceStatus.Pending };
                _db.EventAttendances.Add(row);
            }
            row.ReminderEmailSentAt = now;
        }
        await _db.SaveChangesAsync(ct);

        var emails = await EmailAsync(eventId, e, null, Array.Empty<EventField>(), EventEmailKind.Reminder, due, ct);
        if (emails > 0) _logger.LogInformation("Event {EventId} reminder: {Emails} email(s).", eventId, emails);
        return emails;
    }

    private async Task<List<Kid>> LoadKidsAsync(int eventId, CancellationToken ct)
    {
        var teamId = await _db.ScheduledGames.Where(g => g.Id == eventId).Select(g => g.TeamId).FirstAsync(ct);
        return await _db.TeamPlayers
            .Where(tp => tp.TeamId == teamId)
            .Select(tp => new Kid(
                tp.PlayerId, tp.Player!.FirstName, tp.Player.ParentAccountId,
                tp.Player.ParentAccount!.UserId,
                tp.Player.ParentAccount.User != null ? tp.Player.ParentAccount.User.Email : null,
                tp.Player.ParentAccount.FirstName,
                tp.Player.ParentAccount.Language,
                tp.Player.ParentAccount.NoCommunications,
                tp.Player.ParentAccount.User != null ? tp.Player.ParentAccount.User.GameEmails : EmailPreference.Default,
                tp.Player.ParentAccount.User != null ? tp.Player.ParentAccount.User.EventEmails : EmailPreference.Default))
            .ToListAsync(ct);
    }

    /// <summary>Emails each address (primary parent + guardian contacts) once, kids merged across
    /// families, skipping opted-out families and anyone who chose "Don't email" for this kind of event.</summary>
    private async Task<int> EmailAsync(int eventId, EventSnapshot after, EventSnapshot? before, IReadOnlyList<EventField> changes,
        EventEmailKind kind, IReadOnlyList<Kid> kids, CancellationToken ct)
    {
        var familyIds = kids.Select(k => k.ParentAccountId).Distinct().ToList();
        // A contact linked to a login uses the login's choices (set in the app); otherwise their own.
        var guardianContacts = (await _db.ParentContacts
                .Where(c => familyIds.Contains(c.ParentAccountId) && c.AccessLevel == FamilyAccessLevel.Guardian
                            && c.Email != null && c.Email != "")
                .Select(c => new
                {
                    c.Id, c.ParentAccountId, c.FirstName, c.Email, c.Language, c.UserId,
                    Games = c.User != null ? c.User.GameEmails : c.GameEmails,
                    Events = c.User != null ? c.User.EventEmails : c.EventEmails,
                })
                .ToListAsync(ct))
            .GroupBy(c => c.ParentAccountId)
            .ToDictionary(g => g.Key, g => g.ToList());
        var kidIds = kids.Select(k => k.PlayerId).ToList();
        var answers = await _db.EventAttendances
            .Where(a => a.ScheduledGameId == eventId && kidIds.Contains(a.PlayerId))
            .ToDictionaryAsync(a => a.PlayerId, a => a.Status, ct);

        var recipients = new Dictionary<string, Recipient>(StringComparer.OrdinalIgnoreCase);
        void Add(string? email, string? name, Language lang, PreferenceSubject? subject, EmailPreference games,
            EmailPreference events, Kid kid)
        {
            if (string.IsNullOrWhiteSpace(email)) return;
            if (!NotificationPreferenceRules.WantsEmail(after.Kind, games, events)) return;
            var key = email.Trim();
            if (!recipients.TryGetValue(key, out var r)) recipients[key] = r = new Recipient(name ?? string.Empty, lang, subject, new());
            if (!r.Kids.Any(k => k.Id == kid.PlayerId)) r.Kids.Add((kid.PlayerId, kid.FirstName));
        }
        foreach (var k in kids.Where(k => !k.NoComms))
        {
            Add(k.OwnerEmail, k.OwnerName, k.OwnerLanguage,
                k.OwnerUserId is null ? null : PreferenceSubject.ForUser(k.OwnerUserId), k.OwnerGames, k.OwnerEvents, k);
            if (guardianContacts.TryGetValue(k.ParentAccountId, out var contacts))
                foreach (var c in contacts)
                    Add(c.Email, c.FirstName, c.Language,
                        c.UserId is not null ? PreferenceSubject.ForUser(c.UserId) : PreferenceSubject.ForContact(c.Id),
                        c.Games, c.Events, k);
        }

        var baseUrl = (_app.PublicBaseUrl ?? string.Empty).TrimEnd('/');
        var wordingFor = await EventEmailWording.LoadAllAsync(_db, ct);
        var emails = 0;
        foreach (var (address, r) in recipients)
        {
            var text = EventMessageText.For(r.Lang);
            var kidLinks = r.Kids
                .Select(k => new KidRsvp(k.First, answers.TryGetValue(k.Id, out var s) ? s : AttendanceStatus.Pending,
                    $"{baseUrl}/rsvp?t={Uri.EscapeDataString(_tokens.Create(eventId, k.Id, after.StartsAt))}"))
                .ToList();
            var prefsUrl = r.Subject is null ? null
                : $"{baseUrl}/notification-preferences?t={Uri.EscapeDataString(_prefTokens.Create(r.Subject))}";
            var (subject, plain, html) = EventEmail.Build(text, wordingFor(kind, r.Lang), after, before, changes, r.Name, kidLinks, prefsUrl);
            var result = await _email.SendAsync(address, subject, plain, html, ct);
            if (result.Success) emails++;
            else _logger.LogWarning("Event email to {Email} failed: {Message}", address, result.Message);
        }
        return emails;
    }
}

public record KidRsvp(string FirstName, AttendanceStatus Current, string LinkBase);

// ---------------------------------------------------------------------------------------------
// Wording + formatting (English / Spanish). Times shown in Pacific, where LVSS plays.
// ---------------------------------------------------------------------------------------------

public class EventMessageText
{
    private readonly bool _es;
    private readonly CultureInfo _culture;
    private EventMessageText(bool es) { _es = es; _culture = new CultureInfo(es ? "es-US" : "en-US"); }
    public static EventMessageText For(Language lang) => new(lang == Language.Spanish);

    private static readonly TimeZoneInfo Pacific = ResolvePacific();
    private static TimeZoneInfo ResolvePacific()
    {
        try { return TimeZoneInfo.FindSystemTimeZoneById("America/Los_Angeles"); }
        catch { return TimeZoneInfo.FindSystemTimeZoneById("Pacific Standard Time"); }
    }
    private DateTime Local(DateTime utc) => TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), Pacific);

    public string Date(DateTime utc) => Local(utc).ToString(_es ? "dddd d 'de' MMMM" : "dddd, MMMM d", _culture);
    public string Time(DateTime utc) => Local(utc).ToString("h:mm tt", _culture);
    public string WhenShort(DateTime utc) => Local(utc).ToString(_es ? "ddd d MMM, h:mm tt" : "ddd MMM d, h:mm tt", _culture);
    public string WhenLong(EventSnapshot e) =>
        $"{Date(e.StartsAt)} · {Time(e.StartsAt)}" + (e.EndsAt is DateTime end ? $" – {Time(end)}" : string.Empty);

    public string Kind(ScheduledEventKind k) => k switch
    {
        ScheduledEventKind.Practice => _es ? "Práctica" : "Practice",
        ScheduledEventKind.Miscellaneous => _es ? "Evento" : "Event",
        _ => _es ? "Partido" : "Game",
    };
    public string KindLower(ScheduledEventKind k) => Kind(k).ToLower(_culture);

    public string Title(EventSnapshot e) =>
        e.Kind == ScheduledEventKind.Game
            ? (string.IsNullOrWhiteSpace(e.OpponentName) ? Kind(e.Kind) : (_es ? $"vs {e.OpponentName}" : $"vs {e.OpponentName}"))
            : (string.IsNullOrWhiteSpace(e.Summary) ? Kind(e.Kind) : e.Summary!);

    public string Place(EventSnapshot e) =>
        string.Join(" — ", new[] { e.VenueName, e.Location }.Where(s => !string.IsNullOrWhiteSpace(s)).Distinct());

    public string HomeAway(bool? home) => home switch
    {
        true => _es ? "Local" : "Home",
        false => _es ? "Visitante" : "Away",
        _ => "—",
    };

    public string Shoes(ShoeType s) => s switch
    {
        ShoeType.Cleats => _es ? "Tacos" : "Cleats",
        ShoeType.TurfShoes => _es ? "Zapatos de césped artificial" : "Turf shoes",
        ShoeType.TennisCourtShoes => _es ? "Tenis de cancha" : "Court shoes",
        _ => "—",
    };

    public string FieldLabel(EventField f) => f switch
    {
        EventField.When => _es ? "Fecha y hora" : "Date & time",
        EventField.Arrive => _es ? "Llegar a las" : "Arrive by",
        EventField.Place => _es ? "Lugar" : "Location",
        EventField.Opponent => _es ? "Oponente" : "Opponent",
        EventField.HomeAway => _es ? "Local / Visitante" : "Home / Away",
        EventField.Uniform => _es ? "Uniforme" : "Uniform",
        EventField.Shoes => _es ? "Calzado" : "Shoes",
        EventField.Title => _es ? "Título" : "Title",
        EventField.Notes => _es ? "Notas" : "Notes",
        _ => f.ToString(),
    };

    public string FieldValue(EventField f, EventSnapshot e) => f switch
    {
        EventField.When => WhenLong(e),
        EventField.Arrive => e.ArriveAt is DateTime a ? Time(a) : "—",
        EventField.Place => Or(Place(e) + (string.IsNullOrWhiteSpace(e.VenueAddress) ? string.Empty : $" ({e.VenueAddress})")),
        EventField.Opponent => Or(e.OpponentName),
        EventField.HomeAway => HomeAway(e.IsHome),
        EventField.Uniform => Or(e.Uniform),
        EventField.Shoes => Shoes(e.ShoeType),
        EventField.Title => Title(e),
        EventField.Notes => Or(e.Notes),
        _ => "—",
    };
    private static string Or(string? s) => string.IsNullOrWhiteSpace(s) ? "—" : s.Trim();

    public string Status(AttendanceStatus s) => s switch
    {
        AttendanceStatus.Confirmed => _es ? "Asiste" : "Going",
        AttendanceStatus.Maybe => _es ? "Tal vez" : "Maybe",
        AttendanceStatus.Declined => _es ? "No asiste" : "Not going",
        _ => _es ? "Sin respuesta" : "No answer yet",
    };

    public string Going => _es ? "Asiste" : "Going";
    public string Maybe => _es ? "Tal vez" : "Maybe";
    public string NotGoing => _es ? "No asiste" : "Not going";

    public string WhatChanged => _es ? "Qué cambió" : "What changed";
    public string Was => _es ? "antes" : "was";
    public string Details => _es ? "Detalles" : "Details";
    public string WillAttend(string kid) => _es ? $"¿{kid} asistirá?" : $"Will {kid} be there?";
    public string CurrentAnswer(string status) => _es ? $"Respuesta actual: {status}" : $"Current answer: {status}";
    public string Signature => "Las Vegas Soccer School";
    public string PrefsQuestion => _es ? "¿No quiere recibir estos recordatorios?" : "Don’t want these reminders?";
    public string PrefsLink => _es ? "Actualice sus preferencias aquí." : "Update your preferences here.";

    public string PushNewTitle(EventSnapshot e) => _es ? $"Nuevo en el calendario: {e.TeamName}" : $"New {KindLower(e.Kind)}: {e.TeamName}";
    public string PushNewBody(EventSnapshot e, string names) => _es
        ? $"{Title(e)} · {WhenShort(e.StartsAt)}. Toque para confirmar si asiste {names}."
        : $"{Title(e)} · {WhenShort(e.StartsAt)}. Tap to confirm for {names}.";
    public string PushUpdatedTitle(EventSnapshot e) => _es ? $"Cambio en el calendario: {e.TeamName}" : $"{Kind(e.Kind)} updated: {e.TeamName}";
    public string PushUpdatedBody(EventSnapshot e, IReadOnlyList<EventField> changes) =>
        $"{string.Join(", ", changes.Select(FieldLabel))} · {Title(e)} {WhenShort(e.StartsAt)}";
}

public static class EventEmail
{
    public static (string Subject, string Plain, string Html) Build(
        EventMessageText t, EventEmailWording wording, EventSnapshot e, EventSnapshot? before,
        IReadOnlyList<EventField> changes, string recipientName, IReadOnlyList<KidRsvp> kids, string? preferencesUrl)
    {
        var isUpdate = before is not null;
        var values = EventEmailPlaceholders.Values(t, e, changes, recipientName, kids.Select(k => k.FirstName));
        // A subject is one line: an admin's stray line break would otherwise break the header.
        var subject = System.Text.RegularExpressions.Regex.Replace(EventEmailPlaceholders.Render(wording.Subject, values), @"\s+", " ").Trim();
        var message = EventEmailPlaceholders.Render(wording.Message, values).Trim();
        var footer = EventEmailPlaceholders.Render(wording.Footer, values).Trim();
        static string H(string s) => Esc(s);

        // Details shown for every email: the fields that have a value.
        var detailFields = new List<EventField> { EventField.When };
        if (e.ArriveAt is not null) detailFields.Add(EventField.Arrive);
        if (!string.IsNullOrWhiteSpace(t.Place(e))) detailFields.Add(EventField.Place);
        if (e.Kind == ScheduledEventKind.Game && e.IsHome is not null) detailFields.Add(EventField.HomeAway);
        if (!string.IsNullOrWhiteSpace(e.Uniform)) detailFields.Add(EventField.Uniform);
        if (e.ShoeType != ShoeType.Unspecified) detailFields.Add(EventField.Shoes);
        if (!string.IsNullOrWhiteSpace(e.Notes)) detailFields.Add(EventField.Notes);

        var plain = new StringBuilder();
        plain.AppendLine(message).AppendLine();
        plain.AppendLine($"{t.Kind(e.Kind)}: {t.Title(e)}");
        if (isUpdate)
        {
            plain.AppendLine().AppendLine(t.WhatChanged + ":");
            foreach (var f in changes)
                plain.AppendLine($"- {t.FieldLabel(f)}: {t.FieldValue(f, e)} ({t.Was}: {t.FieldValue(f, before!)})");
        }
        plain.AppendLine().AppendLine(t.Details + ":");
        foreach (var f in detailFields) plain.AppendLine($"- {t.FieldLabel(f)}: {t.FieldValue(f, e)}");
        foreach (var k in kids)
        {
            plain.AppendLine().AppendLine(t.WillAttend(k.FirstName) + " " + t.CurrentAnswer(t.Status(k.Current)));
            plain.AppendLine($"  {t.Going}: {k.LinkBase}&s=going");
            plain.AppendLine($"  {t.Maybe}: {k.LinkBase}&s=maybe");
            plain.AppendLine($"  {t.NotGoing}: {k.LinkBase}&s=no");
        }
        if (footer.Length > 0) plain.AppendLine().AppendLine(footer);
        if (preferencesUrl is not null) plain.AppendLine().AppendLine($"{t.PrefsQuestion} {t.PrefsLink}: {preferencesUrl}");
        plain.AppendLine().AppendLine(t.Signature);

        var html = new StringBuilder();
        html.Append("<!doctype html><html><body style=\"margin:0;background:#f3f5f4;font-family:Arial,Helvetica,sans-serif;color:#1f2a24\">");
        html.Append("<div style=\"max-width:560px;margin:0 auto;padding:24px 16px\">");
        html.Append("<div style=\"background:#0b3d2e;color:#fff;border-radius:10px 10px 0 0;padding:16px 20px\">");
        html.Append($"<div style=\"font-size:12px;letter-spacing:1px;text-transform:uppercase;opacity:.8\">{H(t.Kind(e.Kind))} · {H(e.TeamName)}</div>");
        html.Append($"<div style=\"font-size:22px;font-weight:bold;margin-top:4px\">{H(t.Title(e))}</div>");
        html.Append($"<div style=\"font-size:15px;margin-top:4px\">{H(t.WhenLong(e))}</div></div>");
        html.Append("<div style=\"background:#fff;border-radius:0 0 10px 10px;padding:20px\">");
        html.Append(EventEmailPlaceholders.ToHtmlParagraphs(message, "margin:0 0 12px;line-height:1.45"));

        if (isUpdate && changes.Count > 0)
        {
            html.Append($"<div style=\"background:#fff7e6;border:1px solid #f3d38a;border-radius:8px;padding:12px 14px;margin-bottom:16px\">");
            html.Append($"<div style=\"font-weight:bold;margin-bottom:6px\">{H(t.WhatChanged)}</div>");
            foreach (var f in changes)
                html.Append($"<div style=\"margin:4px 0\"><b>{H(t.FieldLabel(f))}:</b> {H(t.FieldValue(f, e))} "
                    + $"<span style=\"color:#8a6d3b;text-decoration:line-through\">{H(t.FieldValue(f, before!))}</span></div>");
            html.Append("</div>");
        }

        html.Append("<table role=\"presentation\" style=\"width:100%;border-collapse:collapse;margin-bottom:8px\">");
        foreach (var f in detailFields)
            html.Append($"<tr><td style=\"padding:6px 0;color:#5b6b63;font-size:13px;text-transform:uppercase;vertical-align:top;width:40%\">{H(t.FieldLabel(f))}</td>"
                + $"<td style=\"padding:6px 0;font-size:15px\">{H(t.FieldValue(f, e)).Replace("\n", "<br/>")}</td></tr>");
        html.Append("</table>");

        foreach (var k in kids)
        {
            html.Append("<div style=\"border-top:1px solid #e3e8e5;padding-top:14px;margin-top:14px\">");
            html.Append($"<div style=\"font-size:16px;font-weight:bold\">{H(t.WillAttend(k.FirstName))}</div>");
            html.Append($"<div style=\"font-size:13px;color:#5b6b63;margin:2px 0 10px\">{H(t.CurrentAnswer(t.Status(k.Current)))}</div>");
            html.Append(Button(k.LinkBase + "&s=going", t.Going, "#2e7d32"));
            html.Append(Button(k.LinkBase + "&s=maybe", t.Maybe, "#b7791f"));
            html.Append(Button(k.LinkBase + "&s=no", t.NotGoing, "#c62828"));
            html.Append("</div>");
        }
        if (footer.Length > 0)
            html.Append("<div style=\"margin-top:16px\">")
                .Append(EventEmailPlaceholders.ToHtmlParagraphs(footer, "font-size:12px;color:#5b6b63;margin:0 0 6px;line-height:1.45"))
                .Append("</div>");
        if (preferencesUrl is not null)
            html.Append($"<p style=\"font-size:12px;color:#5b6b63;margin:12px 0 0\">{H(t.PrefsQuestion)} "
                + $"<a href=\"{Esc(preferencesUrl)}\" style=\"color:#0b6b4f\">{H(t.PrefsLink)}</a></p>");
        html.Append("</div>");
        html.Append($"<p style=\"text-align:center;font-size:12px;color:#8a978f;margin-top:12px\">{H(t.Signature)}</p>");
        html.Append("</div></body></html>");

        return (subject, plain.ToString(), html.ToString());

        static string Button(string href, string label, string color) =>
            $"<a href=\"{Esc(href)}\" style=\"display:inline-block;background:{color};color:#fff;text-decoration:none;"
            + $"font-weight:bold;font-size:14px;padding:10px 16px;border-radius:6px;margin:0 6px 6px 0\">{Esc(label)}</a>";
    }

    /// <summary>Escapes only the characters that matter in HTML text and attributes, leaving
    /// accented letters readable (the email is sent as UTF-8).</summary>
    public static string Esc(string s) => s
        .Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;").Replace("\"", "&quot;").Replace("'", "&#39;");
}
