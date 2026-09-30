using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
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

    private static EventNotificationSender Sender(Harness h, FakePush push, FakeEmail email) =>
        new(h.Db, push, email, Tokens, Microsoft.Extensions.Options.Options.Create(new AppOptions { PublicBaseUrl = "https://lvss.test" }),
            NullLogger<EventNotificationSender>.Instance);

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
        Assert.StartsWith("Nuevo partido", jose.Subject);
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
