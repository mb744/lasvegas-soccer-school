using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using SoccerSchool.Api.Controllers;
using SoccerSchool.Api.Domain;
using SoccerSchool.Api.Options;
using SoccerSchool.Api.Services;

namespace SoccerSchool.Api.Tests;

public class EventNotificationTests
{
    private sealed class FakeEmail : IEmailSender
    {
        public List<(string To, string Subject, string Plain, string Html)> Sent { get; } = new();
        public bool IsAvailable => true;
        public Task<EmailSendResult> SendAsync(string toEmail, string subject, string body, CancellationToken ct) =>
            SendAsync(toEmail, subject, body, body, ct);
        public Task<EmailSendResult> SendAsync(string toEmail, string subject, string plainText, string html, CancellationToken ct)
        {
            Sent.Add((toEmail, subject, plainText, html));
            return Task.FromResult(new EmailSendResult(true, "id", "ok"));
        }
    }

    private sealed class FakePush : IPushSender
    {
        public List<(List<string> Users, PushNotification Note)> Sent { get; } = new();
        public Task SendToUsersAsync(IEnumerable<string> userIds, PushNotification notification, CancellationToken ct)
        {
            Sent.Add((userIds.ToList(), notification));
            return Task.CompletedTask;
        }
    }

    private static readonly IRsvpTokens Tokens = new RsvpTokens(new EphemeralDataProtectionProvider());
    private static readonly IPreferenceTokens PrefTokens = new PreferenceTokens(new EphemeralDataProtectionProvider());

    private static EventNotificationSender Sender(Harness h, FakePush push, FakeEmail email) =>
        new(h.Db, push, email, Tokens, PrefTokens, Microsoft.Extensions.Options.Options.Create(new AppOptions { PublicBaseUrl = "https://lvss.test" }),
            NullLogger<EventNotificationSender>.Instance);

    /// <summary>The token in an email's "Update your preferences here" link.</summary>
    private static string PrefsToken(string html)
    {
        var m = System.Text.RegularExpressions.Regex.Match(html, @"/notification-preferences\?t=([^""&]+)");
        Assert.True(m.Success, "email has no preferences link");
        return Uri.UnescapeDataString(m.Groups[1].Value);
    }

    /// <summary>Team with two families: Lopez (English owner + Spanish co-parent contact, plus a
    /// guardian login) and an opted-out family (push only).</summary>
    private static async Task<(int TeamId, int GameId, ApplicationUser Mom, ApplicationUser Dad, int AnaId)> SeedAsync(Harness h)
    {
        var team = new Team { Name = "U11 Red" };
        h.Db.Teams.Add(team);
        var mom = await h.UserAsync("mom@test");
        var dad = await h.UserAsync("dad@test");
        var quiet = await h.UserAsync("quiet@test");
        var lopez = new ParentAccount { UserId = mom.Id, FirstName = "Maria", LastName = "Lopez" };
        var optedOut = new ParentAccount { UserId = quiet.Id, FirstName = "Quiet", LastName = "Family", NoCommunications = true };
        h.Db.ParentAccounts.AddRange(lopez, optedOut);
        await h.Db.SaveChangesAsync();

        var ana = new Player { ParentAccountId = lopez.Id, FirstName = "Ana", LastName = "Lopez", DateOfBirth = new DateOnly(2016, 1, 1) };
        var leo = new Player { ParentAccountId = optedOut.Id, FirstName = "Leo", LastName = "Family", DateOfBirth = new DateOnly(2016, 1, 1) };
        h.Db.Players.AddRange(ana, leo);
        await h.Db.SaveChangesAsync();
        h.Db.TeamPlayers.AddRange(new TeamPlayer { TeamId = team.Id, PlayerId = ana.Id }, new TeamPlayer { TeamId = team.Id, PlayerId = leo.Id });
        h.Db.ParentContacts.Add(new ParentContact { ParentAccountId = lopez.Id, FirstName = "Jose", LastName = "Lopez", Email = "jose@test", Language = Language.Spanish });
        h.Db.ParentAccountCollaborators.Add(new ParentAccountCollaborator { ParentAccountId = lopez.Id, UserId = dad.Id, AccessLevel = FamilyAccessLevel.Guardian });

        var game = new ScheduledGame
        {
            TeamId = team.Id, Kind = ScheduledEventKind.Game, ExternalUid = "g1",
            StartsAt = DateTime.UtcNow.AddDays(3).Date.AddHours(1), OpponentName = "Rebels", IsHome = true, Location = "Field 2",
        };
        h.Db.ScheduledGames.Add(game);
        await h.Db.SaveChangesAsync();
        return (team.Id, game.Id, mom, dad, ana.Id);
    }

    [Fact]
    public async Task New_event_pushes_every_family_and_emails_parents_with_answer_links()
    {
        await using var h = new Harness();
        var (_, gameId, mom, dad, _) = await SeedAsync(h);
        var push = new FakePush(); var email = new FakeEmail();

        var result = await Sender(h, push, email).SendAsync(new EventNotificationJob(gameId, null), default);

        // Push: Lopez (owner + guardian login) and the opted-out family (push isn't bulk messaging).
        Assert.Equal(2, result.Pushes);
        Assert.Contains(push.Sent, p => p.Users.Contains(mom.Id) && p.Users.Contains(dad.Id) && p.Note.Title.StartsWith("New game"));

        // Email: Maria (English) and Jose (Spanish); nobody from the opted-out family.
        Assert.Equal(new[] { "jose@test", "mom@test" }, email.Sent.Select(e => e.To).OrderBy(x => x));
        var maria = email.Sent.Single(e => e.To == "mom@test");
        Assert.StartsWith("New game: U11 Red vs Rebels", maria.Subject);
        Assert.Contains("Will Ana be there?", maria.Html);
        Assert.Contains("https://lvss.test/rsvp?t=", maria.Html);
        Assert.Contains("&amp;s=going", maria.Html); // HTML-encoded in the href
        Assert.Contains("&s=maybe", maria.Plain);
        var jose = email.Sent.Single(e => e.To == "jose@test");
        Assert.StartsWith("Nuevo en el calendario: U11 Red vs Rebels", jose.Subject);
        Assert.Contains("¿Ana asistirá?", jose.Html);
    }

    [Fact]
    public async Task Edit_notifies_only_when_something_parents_see_changed_and_says_what()
    {
        await using var h = new Harness();
        var (_, gameId, _, _, _) = await SeedAsync(h);
        var before = (await EventSnapshot.LoadAsync(h.Db, gameId, default))!;

        // Nothing parent-facing changed → nothing sent.
        var push = new FakePush(); var email = new FakeEmail();
        var none = await Sender(h, push, email).SendAsync(new EventNotificationJob(gameId, before), default);
        Assert.Equal((0, 0), (none.Pushes, none.Emails));

        // Move it an hour later and change the field.
        var game = await h.Db.ScheduledGames.SingleAsync(g => g.Id == gameId);
        game.StartsAt = game.StartsAt.AddHours(1);
        game.Location = "Field 5";
        await h.Db.SaveChangesAsync();

        var result = await Sender(h, push, email).SendAsync(new EventNotificationJob(gameId, before), default);
        Assert.Equal(new[] { EventField.When, EventField.Place }, result.Changes);
        var maria = email.Sent.Single(e => e.To == "mom@test");
        Assert.StartsWith("Updated: U11 Red vs Rebels", maria.Subject);
        Assert.Contains("What changed", maria.Html);
        Assert.Contains("Field 5", maria.Html);
        Assert.Contains("Field 2", maria.Html); // the old value, shown struck through
        Assert.Contains(push.Sent, p => p.Note.Title.StartsWith("Game updated") && p.Note.Body.Contains("Date & time, Location"));
    }

    [Fact]
    public async Task Admin_wording_replaces_the_default_for_its_language_and_reset_restores_it()
    {
        await using var h = new Harness();
        var (_, gameId, _, _, _) = await SeedAsync(h);
        var admin = new EventEmailTemplatesController(h.Db)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() },
        };

        Assert.IsType<OkObjectResult>((await admin.Save(EventEmailKind.Created, Language.English, new SaveEventEmailTemplateRequest
        {
            Subject = "Heads up:\n{team.name} {event.title}",
            Message = "Hello {parent.name}!\n\n{players.names} has a {event.type} on {event.date}. Directions: https://lvss.test/map.",
            Footer = "Questions? Reply to this email.",
        }, default)).Result);

        var email = new FakeEmail();
        await Sender(h, new FakePush(), email).SendAsync(new EventNotificationJob(gameId, null), default);
        var maria = email.Sent.Single(e => e.To == "mom@test");
        Assert.Equal("Heads up: U11 Red vs Rebels", maria.Subject); // line break flattened
        Assert.Contains("Hello Maria!", maria.Html);
        Assert.Contains("Ana has a game on", maria.Plain);
        Assert.Contains("<a href=\"https://lvss.test/map\"", maria.Html); // link clickable, period left out
        Assert.Contains("Questions? Reply to this email.", maria.Html);
        Assert.Contains("&amp;s=going", maria.Html); // the answer buttons are always there
        // Jose reads Spanish, which still has the default wording.
        Assert.StartsWith("Nuevo en el calendario", email.Sent.Single(e => e.To == "jose@test").Subject);

        Assert.IsType<NoContentResult>(await admin.Reset(EventEmailKind.Created, Language.English, default));
        email.Sent.Clear();
        await Sender(h, new FakePush(), email).SendAsync(new EventNotificationJob(gameId, null), default);
        Assert.StartsWith("New game: U11 Red vs Rebels", email.Sent.Single(e => e.To == "mom@test").Subject);
    }

    [Fact]
    public async Task Preview_renders_unsaved_wording_and_escapes_it()
    {
        await using var h = new Harness();
        var admin = new EventEmailTemplatesController(h.Db);

        var preview = Assert.IsType<EventEmailPreviewDto>(Assert.IsType<OkObjectResult>(admin.Preview(new EventEmailPreviewRequest
        {
            Kind = EventEmailKind.Updated, Language = Language.Spanish,
            Subject = "Cambio: {team.name}", Message = "Hola {parent.name}: {event.changes} <b>{event.typo}</b>", Footer = "",
        }).Result).Value);
        Assert.Equal("Cambio: U11 Red", preview.Subject);
        Assert.Contains("Hola Maria: Fecha y hora, Lugar", preview.Html);
        Assert.Contains("&lt;b&gt;{event.typo}&lt;/b&gt;", preview.Html); // admin text is escaped; unknown placeholder stays visible
        Assert.Contains("Qué cambió", preview.Html);
    }

    [Fact]
    public async Task Game_and_event_email_choices_are_respected_and_the_email_link_changes_them()
    {
        await using var h = new Harness();
        var (teamId, gameId, mom, _, _) = await SeedAsync(h);

        // Maria turns off game emails in the app; practices still come.
        await NotificationPreferencesStore.SaveAsync(h.Db, PreferenceSubject.ForUser(mom.Id),
            EmailPreference.DontEmail, EmailPreference.Default, null, default);

        var email = new FakeEmail();
        await Sender(h, new FakePush(), email).SendAsync(new EventNotificationJob(gameId, null), default);
        var jose = Assert.Single(email.Sent);
        Assert.Equal("jose@test", jose.To);
        Assert.Contains("¿No quiere recibir estos recordatorios?", jose.Html);

        // Jose (a contact without a login) uses his link to stop game emails too.
        var link = new NotificationPreferencesController(h.Db, PrefTokens);
        var token = PrefsToken(jose.Html);
        var view = Assert.IsType<NotificationPreferencesDto>(Assert.IsType<OkObjectResult>((await link.Get(token, default)).Result).Value);
        Assert.Equal(("Jose", false, EmailPreference.Default), (view.FirstName, view.HasLogin, view.GameEmails));
        Assert.IsType<OkObjectResult>((await link.Save(new SaveNotificationPreferencesByLinkRequest
        {
            Token = token, GameEmails = EmailPreference.DontEmail, EventEmails = EmailPreference.Email,
        }, default)).Result);
        Assert.IsType<BadRequestObjectResult>((await link.Get(token + "x", default)).Result);

        email.Sent.Clear();
        await Sender(h, new FakePush(), email).SendAsync(new EventNotificationJob(gameId, null), default);
        Assert.Empty(email.Sent);

        // A practice still reaches both of them.
        var practice = new ScheduledGame
        {
            TeamId = teamId, Kind = ScheduledEventKind.Practice, ExternalUid = "p1",
            StartsAt = DateTime.UtcNow.AddDays(2), Location = "Field 1",
        };
        h.Db.ScheduledGames.Add(practice);
        await h.Db.SaveChangesAsync();
        await Sender(h, new FakePush(), email).SendAsync(new EventNotificationJob(practice.Id, null), default);
        Assert.Equal(new[] { "jose@test", "mom@test" }, email.Sent.Select(e => e.To).OrderBy(x => x));
        // Maria has a login, so her link opens her account's settings (the same ones as the app).
        Assert.True(PrefTokens.Read(PrefsToken(email.Sent.Single(e => e.To == "mom@test").Html))!.UserId == mom.Id);
    }

    [Fact]
    public async Task Reminder_email_goes_once_per_family_and_muted_logins_get_no_push()
    {
        await using var h = new Harness();
        var (_, gameId, mom, dad, _) = await SeedAsync(h);

        var email = new FakeEmail();
        var sender = Sender(h, new FakePush(), email);
        Assert.Equal(2, await sender.SendReminderAsync(gameId, default));
        Assert.All(email.Sent, e => Assert.Contains("/notification-preferences?t=", e.Html));
        Assert.StartsWith("Reminder: U11 Red vs Rebels", email.Sent.Single(e => e.To == "mom@test").Subject);
        Assert.StartsWith("Recordatorio:", email.Sent.Single(e => e.To == "jose@test").Subject);
        Assert.Equal(0, await sender.SendReminderAsync(gameId, default)); // already reminded

        await NotificationPreferencesStore.SaveAsync(h.Db, PreferenceSubject.ForUser(dad.Id),
            EmailPreference.Default, EmailPreference.Default, false, default);
        Assert.Equal(new[] { mom.Id }, await NotificationPreferenceRules.WithoutMutedAsync(h.Db, new[] { mom.Id, dad.Id }, default));
    }

    [Fact]
    public async Task Every_email_gets_the_recipients_preferences_link_when_we_know_them()
    {
        await using var h = new Harness();
        var (_, _, mom, _, _) = await SeedAsync(h);
        var links = new EmailPreferencesLink(h.Services.GetRequiredService<IServiceScopeFactory>(), PrefTokens,
            Microsoft.Extensions.Options.Options.Create(new AppOptions { PublicBaseUrl = "https://lvss.test/" }));

        // A login: the link opens that account's settings.
        var maria = await links.ForAsync("Mom@Test", default);
        Assert.NotNull(maria);
        Assert.Contains("Don\u2019t want these emails?", maria!.Html);
        Assert.Equal(mom.Id, PrefTokens.Read(PrefsToken(maria.Html))!.UserId);

        // A family contact without a login, in their language.
        var jose = await links.ForAsync("JOSE@test", default);
        Assert.Contains("Actualice sus preferencias aquí.", jose!.Html);
        Assert.NotNull(PrefTokens.Read(PrefsToken(jose.Html))!.ContactId);

        // Someone we don't know has no settings to change.
        Assert.Null(await links.ForAsync("stranger@test", default));

        var (plain, html) = EmailPreferencesLink.AddTo("Hello", "<html><body><p>Hello</p></body></html>", maria);
        Assert.EndsWith(maria.Html + "</body></html>", html);
        Assert.Contains("https://lvss.test/notification-preferences?t=", plain);
        // Event emails already carry it: not added twice.
        Assert.Equal(html, EmailPreferencesLink.AddTo(plain, html, maria).Html);
    }

    [Fact]
    public async Task Cancelled_or_past_events_send_nothing()
    {
        await using var h = new Harness();
        var (_, gameId, _, _, _) = await SeedAsync(h);
        var game = await h.Db.ScheduledGames.SingleAsync(g => g.Id == gameId);
        game.IsCancelled = true;
        await h.Db.SaveChangesAsync();

        var push = new FakePush(); var email = new FakeEmail();
        await Sender(h, push, email).SendAsync(new EventNotificationJob(gameId, null), default);
        Assert.Empty(push.Sent);
        Assert.Empty(email.Sent);
    }

    [Fact]
    public async Task Rsvp_link_records_the_answer_but_respects_coach_answers_and_bad_tokens()
    {
        await using var h = new Harness();
        var (_, gameId, _, _, anaId) = await SeedAsync(h);
        var game = await h.Db.ScheduledGames.AsNoTracking().SingleAsync(g => g.Id == gameId);
        var token = Tokens.Create(gameId, anaId, game.StartsAt);
        var api = new RsvpController(h.Db, Tokens);

        // Reading doesn't change anything.
        var view = Assert.IsType<RsvpDto>(Assert.IsType<OkObjectResult>((await api.Get(token, default)).Result).Value);
        Assert.Equal(("Ana", AttendanceStatus.Pending, true), (view.PlayerFirstName, view.Status, view.CanChange));
        Assert.False(await h.Db.EventAttendances.AnyAsync());

        // Answering records it as the parent's reply.
        var answered = Assert.IsType<RsvpDto>(Assert.IsType<OkObjectResult>(
            (await api.Answer(new RsvpAnswerRequest { Token = token, Answer = "going" }, default)).Result).Value);
        Assert.Equal(AttendanceStatus.Confirmed, answered.Status);
        var row = await h.Db.EventAttendances.AsNoTracking().SingleAsync();
        Assert.Equal((AttendanceStatus.Confirmed, AttendanceSource.ParentReply), (row.Status, row.Source));

        // A coach's answer can't be overridden from the email.
        var tracked = await h.Db.EventAttendances.SingleAsync();
        tracked.Status = AttendanceStatus.Declined; tracked.Source = AttendanceSource.Admin;
        await h.Db.SaveChangesAsync();
        var locked = Assert.IsType<RsvpDto>(Assert.IsType<OkObjectResult>(
            (await api.Answer(new RsvpAnswerRequest { Token = token, Answer = "going" }, default)).Result).Value);
        Assert.True(locked.LockedByCoach);
        Assert.Equal(AttendanceStatus.Declined, (await h.Db.EventAttendances.AsNoTracking().SingleAsync()).Status);

        // Tampered tokens and players no longer on the team are refused.
        Assert.IsType<BadRequestObjectResult>((await api.Get(token + "x", default)).Result);
        Assert.IsType<BadRequestObjectResult>((await api.Get(Tokens.Create(gameId, 99999, game.StartsAt), default)).Result);
        Assert.IsType<BadRequestObjectResult>((await api.Answer(new RsvpAnswerRequest { Token = token, Answer = "yes please" }, default)).Result);
    }
}
