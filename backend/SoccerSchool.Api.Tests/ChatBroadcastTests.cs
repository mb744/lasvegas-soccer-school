using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using SoccerSchool.Api.Controllers;
using SoccerSchool.Api.Domain;
using SoccerSchool.Api.Hubs;
using SoccerSchool.Api.Services;

namespace SoccerSchool.Api.Tests;

public class ChatBroadcastTests
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

    [Fact]
    public async Task Broadcast_posts_to_every_group_and_pushes_each_person_once()
    {
        await using var h = new Harness();
        var admin = await h.UserAsync("admin@test", admin: true);
        var mom = await h.UserAsync("mom@test");
        var dad = await h.UserAsync("dad@test");
        var other = await h.UserAsync("other@test");
        var blocker = await h.UserAsync("blocker@test");

        var lopez = new ParentAccount { UserId = mom.Id, FirstName = "Maria", LastName = "Lopez" };
        var smith = new ParentAccount { UserId = other.Id, FirstName = "Sam", LastName = "Smith" };
        var muted = new ParentAccount { UserId = blocker.Id, FirstName = "Bo", LastName = "Block" };
        h.Db.ParentAccounts.AddRange(lopez, smith, muted);
        await h.Db.SaveChangesAsync();
        h.Db.ParentAccountCollaborators.Add(new ParentAccountCollaborator { ParentAccountId = lopez.Id, UserId = dad.Id, AccessLevel = FamilyAccessLevel.Guardian });

        var a = new ChatGroup { Title = "A Team" };
        var b = new ChatGroup { Title = "B Team" };
        var c = new ChatGroup { Title = "C Team" };
        h.Db.ChatGroups.AddRange(a, b, c);
        await h.Db.SaveChangesAsync();
        h.Db.ChatGroupMembers.AddRange(
            new ChatGroupMember { ChatGroupId = a.Id, ParentAccountId = lopez.Id, DisplayName = "Maria" },
            new ChatGroupMember { ChatGroupId = b.Id, ParentAccountId = lopez.Id, DisplayName = "Maria" },
            new ChatGroupMember { ChatGroupId = b.Id, ParentAccountId = smith.Id, DisplayName = "Sam" },
            new ChatGroupMember { ChatGroupId = b.Id, ParentAccountId = muted.Id, DisplayName = "Bo" },
            new ChatGroupMember { ChatGroupId = c.Id, ParentAccountId = smith.Id, DisplayName = "Sam" },
            new ChatGroupMember { ChatGroupId = a.Id, UserId = admin.Id, DisplayName = "Admin", Role = ChatMemberRole.Admin });
        // Bo blocked the admin: their messages still post, but Bo gets no notification.
        h.Db.ChatUserBlocks.Add(new ChatUserBlock { BlockerUserId = blocker.Id, BlockedUserId = admin.Id });
        await h.Db.SaveChangesAsync();

        var push = new FakePush();
        var chat = new ChatService(h.Db, new FakeHub(), push, new ParentAccountResolver(h.Db, h.Users), new NoStorage());
        var api = new ChatAdminController(h.Db, chat, h.Users)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { User = Harness.Principal(admin) } },
        };

        var all = Assert.IsType<ChatBroadcastResult>(Assert.IsType<OkObjectResult>(
            (await api.Broadcast(new ChatBroadcastRequest { Body = "  Fields closed today  " }, default)).Result).Value);
        Assert.Equal(3, all.Groups);
        Assert.Equal(3, all.People); // Maria, her co-parent, Sam — once each; Bo blocked; the admin is the sender

        foreach (var g in new[] { a, b, c })
            Assert.Equal("Fields closed today", (await h.Db.ChatMessages.SingleAsync(m => m.ChatGroupId == g.Id)).Body);
        Assert.All(await h.Db.ChatMessages.ToListAsync(), m => Assert.True(m.IsFromAdmin));

        var everyone = push.Sent.SelectMany(p => p.Users).ToList();
        Assert.Equal(everyone.Count, everyone.Distinct().Count());
        Assert.DoesNotContain(blocker.Id, everyone);
        Assert.DoesNotContain(admin.Id, everyone);
        // Each push opens the first group (alphabetical) the person is in.
        Assert.Contains(push.Sent, p => p.Users.Contains(mom.Id) && p.Users.Contains(dad.Id) && (int)p.Note.Data!["groupId"] == a.Id);
        Assert.Contains(push.Sent, p => p.Users.Contains(other.Id) && (int)p.Note.Data!["groupId"] == b.Id);

        // Only the picked groups.
        push.Sent.Clear();
        var picked = Assert.IsType<ChatBroadcastResult>(Assert.IsType<OkObjectResult>(
            (await api.Broadcast(new ChatBroadcastRequest { Body = "C only", GroupIds = new() { c.Id, 99999 } }, default)).Result).Value);
        Assert.Equal((1, 1), (picked.Groups, picked.People));
        Assert.Equal(1, await h.Db.ChatMessages.CountAsync(m => m.Body == "C only"));

        Assert.IsType<BadRequestObjectResult>((await api.Broadcast(new ChatBroadcastRequest { Body = "   " }, default)).Result);
    }
}
