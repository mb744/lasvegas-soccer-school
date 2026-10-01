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
        string? overrideName = null, bool? asAdmin = null, MediaAsset? media = null, bool push = true);

    /// <summary>Posts the same admin message into every listed group, then sends each person one
    /// push (not one per group they're in), opening a group they belong to.</summary>
    Task<ChatBroadcastResult> BroadcastAsync(
        IReadOnlyList<int> groupIds, string userId, string senderName, string body, CancellationToken ct);

    /// <summary>Builds the media payload for a message; null when there's no attachment.</summary>
    MobileMediaDto? ToMediaDto(int? mediaId, MediaKind? kind, string? contentType, string? blobName);
}

/// <param name="Groups">Groups the message was posted to.</param>
/// <param name="People">Logins notified (each once, however many of the groups they're in).</param>
public record ChatBroadcastResult(int Groups, int People);

public class ChatService : IChatService
{
    private readonly AppDbContext _db;
    private readonly IHubContext<ChatHub> _hub;
    private readonly IPushSender _push;
    private readonly IParentAccountResolver _accounts;
    private readonly IMediaStorage _storage;

    public ChatService(
        AppDbContext db, IHubContext<ChatHub> hub, IPushSender push,
        IParentAccountResolver accounts, IMediaStorage storage)
    {
        _db = db;
        _hub = hub;
        _push = push;
        _accounts = accounts;
        _storage = storage;
    }

    public MobileMediaDto? ToMediaDto(int? mediaId, MediaKind? kind, string? contentType, string? blobName) =>
        mediaId is int id && kind is MediaKind k && blobName is not null && _storage.IsAvailable
            ? new MobileMediaDto(id, k, contentType ?? "", _storage.GetReadUri(blobName).ToString())
            : null;

    public async Task<List<int>> GetGroupIdsForUserAsync(string userId, CancellationToken ct)
    {
        var account = await _accounts.ResolveGuardianByUserIdAsync(userId, ct);
        var accountId = account?.Id;

        return await _db.ChatGroupMembers
            .Where(m => (accountId != null && m.ParentAccountId == accountId) || m.UserId == userId)
            .Select(m => m.ChatGroupId)
            .Distinct()
            .ToListAsync(ct);
    }

    public async Task<bool> IsMemberAsync(int groupId, string userId, CancellationToken ct)
    {
        var account = await _accounts.ResolveGuardianByUserIdAsync(userId, ct);
        var accountId = account?.Id;
        return await _db.ChatGroupMembers.AnyAsync(
            m => m.ChatGroupId == groupId &&
                 ((accountId != null && m.ParentAccountId == accountId) || m.UserId == userId), ct);
    }

    public async Task<MobileChatMessageDto?> PostMessageAsync(
        int groupId, string userId, string body, CancellationToken ct,
        string? overrideName = null, bool? asAdmin = null, MediaAsset? media = null, bool push = true)
    {
        var account = await _accounts.ResolveGuardianByUserIdAsync(userId, ct);
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
            MediaAssetId = media?.Id,
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
            message.IsFromAdmin, message.Body, message.SentAt,
            ToMediaDto(media?.Id, media?.Kind, media?.ContentType, media?.BlobName));

        // Realtime fan-out to everyone currently connected to the group.
        await _hub.Clients.Group(ChatHub.GroupName(groupId)).SendAsync(ChatHub.ReceiveMessage, dto, ct);

        if (!push) return dto;

        // Push every other member (offline ones get the notification; foreground apps suppress it).
        var recipientUserIds = await PushRecipientsAsync(groupId, userId, ct);
        if (recipientUserIds.Count > 0)
        {
            var title = await _db.ChatGroups.Where(g => g.Id == groupId).Select(g => g.Title).FirstOrDefaultAsync(ct) ?? "New message";
            var text = body.Trim();
            if (text.Length == 0 && media is not null)
                text = media.Kind == MediaKind.Video ? "🎥 Video" : "📷 Photo";
            var preview = text.Length > 120 ? text[..120] + "…" : text;
            await _push.SendToUsersAsync(recipientUserIds, new PushNotification(
                title, $"{senderName}: {preview}",
                new Dictionary<string, object> { ["type"] = "chat", ["groupId"] = groupId }), ct);
        }

        return dto;
    }

    public async Task<ChatBroadcastResult> BroadcastAsync(
        IReadOnlyList<int> groupIds, string userId, string senderName, string body, CancellationToken ct)
    {
        var text = body.Trim();
        // First group (in the admin's order) each person is in: the one their push opens.
        var pushTo = new Dictionary<string, int>();
        var posted = 0;
        foreach (var groupId in groupIds.Distinct())
        {
            var dto = await PostMessageAsync(groupId, userId, text, ct, overrideName: senderName, asAdmin: true, push: false);
            if (dto is null) continue;
            posted++;
            foreach (var u in await PushRecipientsAsync(groupId, userId, ct))
                pushTo.TryAdd(u, groupId);
        }

        var preview = text.Length > 120 ? text[..120] + "…" : text;
        foreach (var byGroup in pushTo.GroupBy(kv => kv.Value))
            await _push.SendToUsersAsync(byGroup.Select(kv => kv.Key).ToList(), new PushNotification(
                "LV Soccer School", $"{senderName}: {preview}",
                new Dictionary<string, object> { ["type"] = "chat", ["groupId"] = byGroup.Key }), ct);

        return new ChatBroadcastResult(posted, pushTo.Count);
    }

    /// <summary>Members to push for a message from <paramref name="senderUserId"/>: everyone but the
    /// sender, minus anyone who has blocked the sender (no notification for muted content).</summary>
    private async Task<List<string>> PushRecipientsAsync(int groupId, string senderUserId, CancellationToken ct)
    {
        var recipientUserIds = (await GetMemberUserIdsAsync(groupId, ct)).Where(id => id != senderUserId).ToList();
        if (recipientUserIds.Count == 0) return recipientUserIds;
        var blockedBy = await _db.ChatUserBlocks
            .Where(b => b.BlockedUserId == senderUserId && recipientUserIds.Contains(b.BlockerUserId))
            .Select(b => b.BlockerUserId)
            .ToListAsync(ct);
        return blockedBy.Count > 0 ? recipientUserIds.Except(blockedBy).ToList() : recipientUserIds;
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
                .Where(c => parentAccountIds.Contains(c.ParentAccountId) && c.AccessLevel == FamilyAccessLevel.Guardian)
                .Select(c => c.UserId)
                .ToListAsync(ct);
            foreach (var u in collaborators) if (!string.IsNullOrEmpty(u)) userIds.Add(u);
        }

        return userIds.ToList();
    }
}
