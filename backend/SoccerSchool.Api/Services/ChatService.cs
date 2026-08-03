using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using SoccerSchool.Api.Data;
using SoccerSchool.Api.Domain;
using SoccerSchool.Api.Dtos;
using SoccerSchool.Api.Hubs;

namespace SoccerSchool.Api.Services;

/// <summary>
/// Core native-chat logic shared by the SignalR <see cref="ChatHub"/> (realtime sends) and the REST
/// controllers (history, admin posts). Owns membership resolution, message persistence, the SignalR
/// fan-out, and the offline-member push. Keeping it here means a message posted by an admin from the
/// web and one posted by a parent from the app travel the identical path.
/// </summary>
public interface IChatService
{
    /// <summary>The chat group ids this login can see — membership is on the family, so the owner and
    /// any linked collaborator both match a parent membership; admins match by user id.</summary>
    Task<List<int>> GetGroupIdsForUserAsync(string userId, CancellationToken ct);

    Task<bool> IsMemberAsync(int groupId, string userId, CancellationToken ct);

    /// <summary>Persists a message from a member, broadcasts it to the group, and pushes offline
    /// members. Returns null if the sender isn't a member of the group. <paramref name="overrideName"/>
    /// and <paramref name="asAdmin"/> let an admin post from the web without a ChatGroupMember row.</summary>
    Task<MobileChatMessageDto?> PostMessageAsync(
        int groupId, string userId, string body, CancellationToken ct,
        string? overrideName = null, bool? asAdmin = null);
}

public class ChatService : IChatService
{
    private readonly AppDbContext _db;
    private readonly IHubContext<ChatHub> _hub;
    private readonly IPushSender _push;
    private readonly IParentAccountResolver _accounts;

    public ChatService(AppDbContext db, IHubContext<ChatHub> hub, IPushSender push, IParentAccountResolver accounts)
    {
        _db = db;
        _hub = hub;
        _push = push;
        _accounts = accounts;
    }

    public async Task<List<int>> GetGroupIdsForUserAsync(string userId, CancellationToken ct)
    {
        var account = await _accounts.ResolveByUserIdAsync(userId, ct);
        var accountId = account?.Id;

        return await _db.ChatGroupMembers
            .Where(m => (accountId != null && m.ParentAccountId == accountId) || m.UserId == userId)
            .Select(m => m.ChatGroupId)
            .Distinct()
            .ToListAsync(ct);
    }

    public async Task<bool> IsMemberAsync(int groupId, string userId, CancellationToken ct)
    {
        var account = await _accounts.ResolveByUserIdAsync(userId, ct);
        var accountId = account?.Id;
        return await _db.ChatGroupMembers.AnyAsync(
            m => m.ChatGroupId == groupId &&
                 ((accountId != null && m.ParentAccountId == accountId) || m.UserId == userId), ct);
    }

    public async Task<MobileChatMessageDto?> PostMessageAsync(
        int groupId, string userId, string body, CancellationToken ct,
        string? overrideName = null, bool? asAdmin = null)
    {
        var account = await _accounts.ResolveByUserIdAsync(userId, ct);
        var accountId = account?.Id;

        var member = await _db.ChatGroupMembers.FirstOrDefaultAsync(
            m => m.ChatGroupId == groupId &&
                 ((accountId != null && m.ParentAccountId == accountId) || m.UserId == userId), ct);

        // Admins posting from the web may not have a membership row; allow with an explicit override.
        if (member is null && overrideName is null) return null;

        var senderName = overrideName ?? member!.DisplayName;
        var isFromAdmin = asAdmin ?? member!.Role == ChatMemberRole.Admin;

        var message = new ChatMessage
        {
            ChatGroupId = groupId,
            SenderUserId = userId,
            SenderName = senderName,
            IsFromAdmin = isFromAdmin,
            Body = body.Trim(),
            SentAt = DateTime.UtcNow,
        };
        _db.ChatMessages.Add(message);

        // The sender has implicitly read their own message.
        if (member is not null)
        {
            await _db.SaveChangesAsync(ct); // assign message.Id first
            member.LastReadMessageId = message.Id;
        }
        await _db.SaveChangesAsync(ct);

        var dto = new MobileChatMessageDto(
            message.Id, groupId, message.SenderUserId, message.SenderName,
            message.IsFromAdmin, message.Body, message.SentAt);

        // Realtime fan-out to everyone currently connected to the group.
        await _hub.Clients.Group(ChatHub.GroupName(groupId)).SendAsync(ChatHub.ReceiveMessage, dto, ct);

        // Push every other member (offline ones get the notification; foreground apps suppress it).
        var recipientUserIds = (await GetMemberUserIdsAsync(groupId, ct)).Where(id => id != userId).ToList();
        if (recipientUserIds.Count > 0)
        {
            var title = await _db.ChatGroups.Where(g => g.Id == groupId).Select(g => g.Title).FirstOrDefaultAsync(ct) ?? "New message";
            var preview = body.Length > 120 ? body[..120] + "…" : body;
            await _push.SendToUsersAsync(recipientUserIds, new PushNotification(
                title, $"{senderName}: {preview}",
                new Dictionary<string, object> { ["type"] = "chat", ["groupId"] = groupId }), ct);
        }

        return dto;
    }

    /// <summary>Every login that should receive notifications for the group: each parent member's
    /// family logins (owner + collaborators) plus any admin members' user ids.</summary>
    private async Task<List<string>> GetMemberUserIdsAsync(int groupId, CancellationToken ct)
    {
        var members = await _db.ChatGroupMembers
            .Where(m => m.ChatGroupId == groupId)
            .Select(m => new { m.ParentAccountId, m.UserId })
            .ToListAsync(ct);

        var parentAccountIds = members.Where(m => m.ParentAccountId != null).Select(m => m.ParentAccountId!.Value).ToList();
        var userIds = new HashSet<string>(members.Where(m => m.UserId != null).Select(m => m.UserId!));

        if (parentAccountIds.Count > 0)
        {
            var owners = await _db.ParentAccounts
                .Where(a => parentAccountIds.Contains(a.Id))
                .Select(a => a.UserId)
                .ToListAsync(ct);
            foreach (var u in owners) if (!string.IsNullOrEmpty(u)) userIds.Add(u);

            var collaborators = await _db.ParentAccountCollaborators
                .Where(c => parentAccountIds.Contains(c.ParentAccountId))
                .Select(c => c.UserId)
                .ToListAsync(ct);
            foreach (var u in collaborators) if (!string.IsNullOrEmpty(u)) userIds.Add(u);
        }

        return userIds.ToList();
    }
}
