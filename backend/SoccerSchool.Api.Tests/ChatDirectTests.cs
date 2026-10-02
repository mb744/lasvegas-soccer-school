using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using SoccerSchool.Api.Controllers;
using SoccerSchool.Api.Controllers.Mobile;
using SoccerSchool.Api.Domain;
using SoccerSchool.Api.Dtos;
using SoccerSchool.Api.Hubs;
using SoccerSchool.Api.Services;

namespace SoccerSchool.Api.Tests;

public class ChatDirectTests
{
    private sealed class FakePush : IPushSender
    {
        public List<(List<string> Users, PushNotification Note)> Sent { get; } = new();
        public Task SendToUsersAsync(IEnumerable<string> userIds, PushNotification notification, CancellationToken ct)
        {
            Sent.Add((userIds.ToList(), notification));
            return Task.CompletedTask;
        }
    }

    private sealed class NullClientProxy : IClientProxy
    {
        public Task SendCoreAsync(string method, object?[] args, CancellationToken ct = default) => Task.CompletedTask;
    }

    private sealed class FakeHub : IHubContext<ChatHub>
    {
        public IHubClients Clients { get; } = new NullClients();
        public IGroupManager Groups => throw new NotSupportedException();
    }

    private sealed class NullClients : IHubClients
    {
        private static readonly IClientProxy Proxy = new NullClientProxy();
        public IClientProxy All => Proxy;
        public IClientProxy AllExcept(IReadOnlyList<string> excludedConnectionIds) => Proxy;
        public IClientProxy Client(string connectionId) => Proxy;
        public IClientProxy Clients(IReadOnlyList<string> connectionIds) => Proxy;
        public IClientProxy Group(string groupName) => Proxy;
        public IClientProxy GroupExcept(string groupName, IReadOnlyList<string> excludedConnectionIds) => Proxy;
        public IClientProxy Groups(IReadOnlyList<string> groupNames) => Proxy;
        public IClientProxy User(string userId) => Proxy;
        public IClientProxy Users(IReadOnlyList<string> userIds) => Proxy;
    }

    private sealed class NoStorage : IMediaStorage
    {
        public bool IsAvailable => false;
        public string? ValidateUpload(MediaKind kind, string contentType, long sizeBytes) => throw new NotSupportedException();
        public string NewBlobName(string contentType) => throw new NotSupportedException();
        public Task<Uri> GetUploadUriAsync(string blobName, CancellationToken ct) => throw new NotSupportedException();
        public Task<MediaVerifyResult> VerifyAsync(string blobName, MediaKind kind, CancellationToken ct) => throw new NotSupportedException();
        public Uri GetReadUri(string blobName) => throw new NotSupportedException();
        public Task DeleteAsync(string blobName, CancellationToken ct) => throw new NotSupportedException();
    }

    private static MobileChatController Api(Harness h, ChatService chat, ApplicationUser as_) =>
        new(h.Db, chat, new ParentAccountResolver(h.Db, h.Users), h.Users)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { User = Harness.Principal(as_) } },
        };

    [Fact]
    public async Task Members_see_everyone_and_can_privately_message_anyone_in_the_group()
    {
        await using var h = new Harness();
        var admin = await h.UserAsync("admin@test", admin: true);
        var coach = await h.UserAsync("coach@test");
        var mom = await h.UserAsync("mom@test");
        var dad = await h.UserAsync("dad@test");
        var sam = await h.UserAsync("sam@test");
        var outsider = await h.UserAsync("out@test");

        var team = new Team { Name = "U11 Red" };
        var lopez = new ParentAccount { UserId = mom.Id, FirstName = "Maria", LastName = "Lopez" };
        var smith = new ParentAccount { UserId = sam.Id, FirstName = "Sam", LastName = "Smith" };
        h.Db.AddRange(team, lopez, smith);
        await h.Db.SaveChangesAsync();
        h.Db.Players.Add(new Player { ParentAccountId = lopez.Id, FirstName = "Ana", LastName = "Lopez", DateOfBirth = new DateOnly(2016, 1, 1) });
        h.Db.ParentAccountCollaborators.Add(new ParentAccountCollaborator { ParentAccountId = lopez.Id, UserId = dad.Id, AccessLevel = FamilyAccessLevel.Guardian });
        h.Db.ParentContacts.Add(new ParentContact { ParentAccountId = lopez.Id, FirstName = "Jose", LastName = "Lopez", UserId = dad.Id });
        h.Db.TeamCoaches.Add(new TeamCoach { TeamId = team.Id, Name = "Coach Carla", UserId = coach.Id });
        var group = new ChatGroup { Title = "U11 Red chat", TeamId = team.Id };
        h.Db.ChatGroups.Add(group);
        await h.Db.SaveChangesAsync();
        h.Db.ChatGroupMembers.AddRange(
            new ChatGroupMember { ChatGroupId = group.Id, ParentAccountId = lopez.Id, DisplayName = "Maria Lopez" },
            new ChatGroupMember { ChatGroupId = group.Id, ParentAccountId = smith.Id, DisplayName = "Sam Smith" },
            new ChatGroupMember { ChatGroupId = group.Id, UserId = admin.Id, DisplayName = "Club Admin", Role = ChatMemberRole.Admin },
            new ChatGroupMember { ChatGroupId = group.Id, UserId = coach.Id, DisplayName = "Coach Carla" });
        h.Db.MobileAppInstalls.Add(new MobileAppInstall { InstallationId = "i1", UserId = coach.Id });
        await h.Db.SaveChangesAsync();

        var push = new FakePush();
        var chat = new ChatService(h.Db, new FakeHub(), push, new ParentAccountResolver(h.Db, h.Users), new NoStorage());

        // Everyone in the group, staff first; the co-parent appears as a person of their own.
        var people = Assert.IsAssignableFrom<IEnumerable<MobileChatPersonDto>>(
            Assert.IsType<OkObjectResult>((await Api(h, chat, mom).Members(group.Id, default)).Result).Value).ToList();
        Assert.Equal(new[] { "Club Admin", "Coach Carla", "Jose Lopez", "Maria Lopez", "Sam Smith" }, people.Select(p => p.Name));
        Assert.True(people.Single(p => p.Name == "Club Admin").IsAdmin);
        var carla = people.Single(p => p.Name == "Coach Carla");
        Assert.True(carla.IsCoach && carla.OnApp);
        Assert.True(people.Single(p => p.Name == "Maria Lopez").IsYou);
        Assert.Equal("Ana", people.Single(p => p.Name == "Jose Lopez").Detail);
        // Not in the group: can't see who's in it.
        Assert.IsType<ForbidResult>((await Api(h, chat, outsider).Members(group.Id, default)).Result);

        // Maria messages the coach; either side opening it lands in the same chat.
        var open = Assert.IsType<OpenDirectChatResult>(Assert.IsType<OkObjectResult>(
            (await Api(h, chat, mom).OpenDirect(new OpenDirectChatRequest(coach.Id, group.Id), default)).Result).Value);
        var again = Assert.IsType<OpenDirectChatResult>(Assert.IsType<OkObjectResult>(
            (await Api(h, chat, coach).OpenDirect(new OpenDirectChatRequest(mom.Id, group.Id), default)).Result).Value);
        Assert.Equal(open.GroupId, again.GroupId);

        // It's titled with the other person's name, and only the two of them have it.
        var momChats = Assert.IsAssignableFrom<IEnumerable<MobileChatGroupDto>>(
            Assert.IsType<OkObjectResult>((await Api(h, chat, mom).Groups(default)).Result).Value).ToList();
        Assert.Contains(momChats, c => c.Id == open.GroupId && c.IsDirect && c.Title == "Coach Carla");
        var dadChats = Assert.IsAssignableFrom<IEnumerable<MobileChatGroupDto>>(
            Assert.IsType<OkObjectResult>((await Api(h, chat, dad).Groups(default)).Result).Value).ToList();
        Assert.DoesNotContain(dadChats, c => c.Id == open.GroupId); // the co-parent sees the team chat, not Maria's DM
        Assert.Contains(dadChats, c => c.Id == group.Id);
        Assert.False(await h.Db.ChatGroups.Where(g => !g.IsDirect).AnyAsync(g => g.Id == open.GroupId)); // hidden from admin lists

        // A message notifies only the other person, titled with the sender.
        await chat.PostMessageAsync(open.GroupId, mom.Id, "Can Ana come late Saturday?", default);
        var note = Assert.Single(push.Sent);
        Assert.Equal(new[] { coach.Id }, note.Users);
        Assert.Equal(("Maria Lopez", "Can Ana come late Saturday?"), (note.Note.Title, note.Note.Body));

        // Blocked either way: no new chat. Someone outside the group can't be messaged through it.
        h.Db.ChatUserBlocks.Add(new ChatUserBlock { BlockerUserId = sam.Id, BlockedUserId = mom.Id });
        await h.Db.SaveChangesAsync();
        Assert.IsType<BadRequestObjectResult>((await Api(h, chat, mom).OpenDirect(new OpenDirectChatRequest(sam.Id, group.Id), default)).Result);
        Assert.IsType<ForbidResult>((await Api(h, chat, mom).OpenDirect(new OpenDirectChatRequest(outsider.Id, group.Id), default)).Result);
        Assert.IsType<ForbidResult>((await Api(h, chat, outsider).OpenDirect(new OpenDirectChatRequest(mom.Id, group.Id), default)).Result);
    }
}
