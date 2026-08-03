using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.SignalR;
using SoccerSchool.Api.Domain;
using SoccerSchool.Api.Services;

namespace SoccerSchool.Api.Hubs;

/// <summary>
/// Real-time group chat for the mobile app. Authenticated with the mobile JWT scheme (the access
/// token is passed as the <c>access_token</c> query param for the websocket handshake — wired up in
/// Program.cs). On connect, the caller is auto-joined to a SignalR group per <see cref="ChatGroup"/>
/// they belong to (<c>chat-{groupId}</c>). Sending persists the message and fans it out to that
/// group; offline members get a push. History/pagination is REST (<c>MobileChatController</c>).
/// </summary>
[Authorize(AuthenticationSchemes = AuthSchemes.MobileJwt)]
public class ChatHub : Hub
{
    /// <summary>Client method name clients subscribe to for new messages.</summary>
    public const string ReceiveMessage = "ReceiveMessage";

    public static string GroupName(int chatGroupId) => $"chat-{chatGroupId}";

    private readonly IChatService _chat;
    private readonly UserManager<ApplicationUser> _users;

    public ChatHub(IChatService chat, UserManager<ApplicationUser> users)
    {
        _chat = chat;
        _users = users;
    }

    public override async Task OnConnectedAsync()
    {
        var userId = _users.GetUserId(Context.User!);
        if (!string.IsNullOrEmpty(userId))
        {
            var groupIds = await _chat.GetGroupIdsForUserAsync(userId, Context.ConnectionAborted);
            foreach (var gid in groupIds)
                await Groups.AddToGroupAsync(Context.ConnectionId, GroupName(gid));
        }
        await base.OnConnectedAsync();
    }

    /// <summary>Post a message to a group the caller belongs to. Persists, fans out to the SignalR
    /// group, and pushes offline members. Throws <see cref="HubException"/> if the caller isn't a member.</summary>
    public async Task SendMessage(int groupId, string body)
    {
        var userId = _users.GetUserId(Context.User!);
        if (string.IsNullOrEmpty(userId)) throw new HubException("Not authenticated.");
        if (string.IsNullOrWhiteSpace(body)) return;
        if (body.Length > 4000) body = body[..4000];

        var dto = await _chat.PostMessageAsync(groupId, userId, body, Context.ConnectionAborted);
        if (dto is null) throw new HubException("You are not a member of this group.");
        // PostMessageAsync already broadcast to the group + pushed offline members.
    }
}
