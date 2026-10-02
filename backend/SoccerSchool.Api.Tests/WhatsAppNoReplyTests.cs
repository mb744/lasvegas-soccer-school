using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using SoccerSchool.Api.Controllers;
using SoccerSchool.Api.Domain;
using SoccerSchool.Api.Dtos;
using SoccerSchool.Api.Options;
using SoccerSchool.Api.Services;

namespace SoccerSchool.Api.Tests;

public class WhatsAppNoReplyTests
{
    private sealed class FakeSender : IMessageSender
    {
        public List<string> To { get; } = new();
        public bool IsAvailable(MessageChannel channel) => true;
        public Task<MessageSendResult> SendAsync(MessageChannel channel, string toPhone, string body, CancellationToken ct)
        {
            To.Add(toPhone);
            return Task.FromResult(new MessageSendResult(true, "SM1", MessageDeliveryStatus.Sent, "ok"));
        }
        public Task<MessageSendResult> SendTemplateAsync(string toPhone, string contentSid, IReadOnlyDictionary<string, string> variables, CancellationToken ct)
        {
            To.Add(toPhone);
            return Task.FromResult(new MessageSendResult(true, "SM1", MessageDeliveryStatus.Sent, "ok"));
        }
    }

    [Fact]
    public async Task WhatsApp_about_an_event_skips_families_who_already_answered_except_when_turned_off_or_cancelled()
    {
        await using var h = new Harness();
        var team = new Team { Name = "U11 Red" };
        h.Db.Teams.Add(team);
        var phones = new Dictionary<string, string> { ["answered"] = "+17025550001", ["pending"] = "+17025550002", ["norow"] = "+17025550003" };
        var kids = new Dictionary<string, Player>();
        foreach (var (key, phone) in phones)
        {
            var user = await h.UserAsync($"{key}@test");
            var family = new ParentAccount { UserId = user.Id, FirstName = key, LastName = "Family", CellPhone = phone, HasWhatsApp = true };
            h.Db.ParentAccounts.Add(family);
            await h.Db.SaveChangesAsync();
            var kid = new Player { ParentAccountId = family.Id, FirstName = key, LastName = "Kid", DateOfBirth = new DateOnly(2016, 1, 1) };
            h.Db.Players.Add(kid);
            await h.Db.SaveChangesAsync();
            h.Db.TeamPlayers.Add(new TeamPlayer { TeamId = team.Id, PlayerId = kid.Id });
            kids[key] = kid;
        }
        var game = new ScheduledGame { TeamId = team.Id, Kind = ScheduledEventKind.Game, ExternalUid = "g1", StartsAt = DateTime.UtcNow.AddDays(2), OpponentName = "Rebels" };
        h.Db.ScheduledGames.Add(game);
        await h.Db.SaveChangesAsync();
        h.Db.EventAttendances.AddRange(
            new EventAttendance { ScheduledGameId = game.Id, PlayerId = kids["answered"].Id, Status = AttendanceStatus.Confirmed },
            new EventAttendance { ScheduledGameId = game.Id, PlayerId = kids["pending"].Id, Status = AttendanceStatus.Pending });
        await h.Db.SaveChangesAsync();

        var sender = new FakeSender();
        var app = Microsoft.Extensions.Options.Options.Create(new AppOptions());
        var api = new MessagingController(h.Db, sender, null!, new RecipientResolver(h.Db, app), null!, null!, null!,
            Microsoft.Extensions.Options.Options.Create(new TwilioOptions()), app, NullLogger<MessagingController>.Instance);

        async Task<List<string>> Send(bool? onlyNoReply)
        {
            sender.To.Clear();
            var r = await api.CreateBroadcast(new CreateBroadcastRequest
            {
                Channel = MessageChannel.WhatsApp,
                BodyEn = "Is your player coming Saturday?",
                ScheduledGameId = game.Id,
                OnlyNoReply = onlyNoReply,
                Target = new BroadcastTargetDto { Kind = RecipientTargetKindDto.DynamicGroup, DynamicGroupKey = $"team-{team.Id}" },
            }, default);
            Assert.IsType<OkObjectResult>(r.Result);
            return sender.To.OrderBy(x => x).ToList();
        }

        // Default: only the two families without an answer (a Pending row counts as no answer).
        Assert.Equal(new[] { phones["pending"], phones["norow"] }.OrderBy(x => x), await Send(null));
        // Turned off: everyone.
        Assert.Equal(3, (await Send(false)).Count);

        // Once everyone has answered there's nobody to send to.
        h.Db.EventAttendances.Single(a => a.PlayerId == kids["pending"].Id).Status = AttendanceStatus.Declined;
        h.Db.EventAttendances.Add(new EventAttendance { ScheduledGameId = game.Id, PlayerId = kids["norow"].Id, Status = AttendanceStatus.Maybe });
        await h.Db.SaveChangesAsync();
        Assert.IsType<BadRequestObjectResult>((await api.CreateBroadcast(new CreateBroadcastRequest
        {
            Channel = MessageChannel.WhatsApp, BodyEn = "Reminder", ScheduledGameId = game.Id,
            Target = new BroadcastTargetDto { Kind = RecipientTargetKindDto.DynamicGroup, DynamicGroupKey = $"team-{team.Id}" },
        }, default)).Result);

        // A cancelled event's message goes to everyone, whatever they answered.
        game.IsCancelled = true;
        await h.Db.SaveChangesAsync();
        Assert.Equal(3, (await Send(null)).Count);
    }
}
