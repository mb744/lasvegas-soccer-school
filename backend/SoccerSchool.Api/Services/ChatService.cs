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

    /// <summary>Everyone in a group as people (logins): each member family's parent and guardian
    /// logins, plus admin/staff members. Staff first, then parents by name.</summary>
    Task<List<MobileChatPersonDto>> GetPeopleAsync(int groupId, string viewerUserId, CancellationToken ct);

    /// <summary>Finds or creates the private chat between the caller and someone in a group they
    /// both belong to.</summary>
    Task<DirectChatOutcome> OpenDirectAsync(string callerUserId, string targetUserId, int viaGroupId, CancellationToken ct);
}

/// <param name="Forbidden">The caller isn't in the group, or the person isn't in it.</param>
public record DirectChatOutcome(int? GroupId, string? Error = null, bool Forbidden = false);

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
        // An archived family (account deleted, or every kid archived) is out of the group chats.
        var accountId = account is not null && !await FamilyArchive.IsArchivedAsync(_db, account.Id, ct) ? account.Id : (int?)null;

        return await _db.ChatGroupMembers
            .Where(m => (accountId != null && m.ParentAccountId == accountId) || m.UserId == userId)
            .Select(m => m.ChatGroupId)
            .Distinct()
            .ToListAsync(ct);
    }

    public async Task<bool> IsMemberAsync(int groupId, string userId, CancellationToken ct)
    {
        var account = await _accounts.ResolveGuardianByUserIdAsync(userId, ct);
        // An archived family (account deleted, or every kid archived) is out of the group chats.
        var accountId = account is not null && !await FamilyArchive.IsArchivedAsync(_db, account.Id, ct) ? account.Id : (int?)null;
        return await _db.ChatGroupMembers.AnyAsync(
            m => m.ChatGroupId == groupId &&
                 ((accountId != null && m.ParentAccountId == accountId) || m.UserId == userId), ct);
    }

    public async Task<MobileChatMessageDto?> PostMessageAsync(
        int groupId, string userId, string body, CancellationToken ct,
        string? overrideName = null, bool? asAdmin = null, MediaAsset? media = null, bool push = true)
    {
        var account = await _accounts.ResolveGuardianByUserIdAsync(userId, ct);
        // An archived family (account deleted, or every kid archived) is out of the group chats.
        var accountId = account is not null && !await FamilyArchive.IsArchivedAsync(_db, account.Id, ct) ? account.Id : (int?)null;

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
            var group = await _db.ChatGroups.Where(g => g.Id == groupId).Select(g => new { g.Title, g.IsDirect }).FirstOrDefaultAsync(ct);
            var text = body.Trim();
            if (text.Length == 0 && media is not null)
                text = media.Kind == MediaKind.Video ? "🎥 Video" : "📷 Photo";
            var preview = text.Length > 120 ? text[..120] + "…" : text;
            // A direct message reads like a text: the sender is the title.
            await _push.SendToUsersAsync(recipientUserIds, new PushNotification(
                group?.IsDirect == true ? senderName : group?.Title ?? "New message",
                group?.IsDirect == true ? preview : $"{senderName}: {preview}",
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

    public async Task<List<MobileChatPersonDto>> GetPeopleAsync(int groupId, string viewerUserId, CancellationToken ct) =>
        (await LoadPeopleAsync(groupId, viewerUserId, ct)).People;

    /// <summary>The group's people, plus which login each duplicate login is shown as. A parent
    /// who has a second login linked to the same family (e.g. signed up again with another email)
    /// shows once: logins in one family with the same name or phone are one person, shown as the
    /// login they used most recently (or as "you" when the viewer is one of them).</summary>
    private async Task<(List<MobileChatPersonDto> People, Dictionary<string, string> ShownAs)> LoadPeopleAsync(
        int groupId, string viewerUserId, CancellationToken ct)
    {
        var members = await _db.ChatGroupMembers
            .Where(m => m.ChatGroupId == groupId)
            .Select(m => new { m.ParentAccountId, m.UserId, m.DisplayName, m.Role })
            .ToListAsync(ct);

        var familyIds = members.Where(m => m.ParentAccountId != null).Select(m => m.ParentAccountId!.Value).Distinct().ToList();
        // Archived families (account deleted, or every kid archived) aren't shown or messageable.
        var archivedFamilies = await FamilyArchive.ArchivedIdsAsync(_db, ct);
        var families = await _db.ParentAccounts
            .Where(a => familyIds.Contains(a.Id) && !archivedFamilies.Contains(a.Id))
            .Select(a => new
            {
                a.Id, a.UserId, a.FirstName, a.LastName, a.CellPhone,
                Kids = a.Players.OrderBy(p => p.FirstName).Select(p => p.FirstName).ToList(),
            })
            .ToListAsync(ct);
        var guardians = await _db.ParentAccountCollaborators
            .Where(c => familyIds.Contains(c.ParentAccountId) && c.AccessLevel == FamilyAccessLevel.Guardian)
            .Select(c => new { c.ParentAccountId, c.UserId })
            .ToListAsync(ct);
        var guardianIds = guardians.Select(g => g.UserId).ToList();
        var guardianContacts = (await _db.ParentContacts
                .Where(c => c.UserId != null && guardianIds.Contains(c.UserId))
                .Select(c => new { c.UserId, c.FirstName, c.LastName, c.CellPhone })
                .ToListAsync(ct))
            .GroupBy(c => c.UserId!)
            .ToDictionary(g => g.Key, g => (Name: $"{g.First().FirstName} {g.First().LastName}".Trim(), Phone: g.First().CellPhone));

        var people = new Dictionary<string, (string Name, string? Detail, bool Staff)>();
        var familyOf = new Dictionary<string, int>();
        var phoneOf = new Dictionary<string, string?>();
        void Add(string? userId, string name, string? detail, bool staff, int? familyId = null, string? phone = null)
        {
            if (string.IsNullOrEmpty(userId)) return;
            // Someone in the group both as staff and through a family shows once, as staff.
            if (people.TryGetValue(userId, out var existing) && (existing.Staff || !staff)) return;
            people[userId] = (string.IsNullOrWhiteSpace(name) ? "—" : name, detail, staff);
            if (staff) familyOf.Remove(userId);
            else if (familyId is int f) { familyOf[userId] = f; phoneOf[userId] = phone; }
        }
        foreach (var m in members.Where(m => m.UserId != null))
            Add(m.UserId, m.DisplayName, null, staff: true);
        foreach (var f in families)
        {
            var kids = f.Kids.Count > 0 ? string.Join(", ", f.Kids) : null;
            var familyName = $"{f.FirstName} {f.LastName}".Trim();
            Add(f.UserId, familyName, kids, staff: false, f.Id, f.CellPhone);
            foreach (var g in guardians.Where(g => g.ParentAccountId == f.Id))
            {
                var contact = guardianContacts.TryGetValue(g.UserId, out var c) ? c : (Name: familyName, Phone: (string?)null);
                Add(g.UserId, contact.Name, kids, staff: false, f.Id, contact.Phone);
            }
        }

        var ids = people.Keys.ToList();
        var adminRoleId = await _db.Roles.Where(r => r.Name == Roles.Admin).Select(r => r.Id).FirstOrDefaultAsync(ct);
        var adminIds = adminRoleId is null
            ? new HashSet<string>()
            : (await _db.UserRoles.Where(ur => ur.RoleId == adminRoleId && ids.Contains(ur.UserId)).Select(ur => ur.UserId).ToListAsync(ct)).ToHashSet();
        var coachIds = (await _db.TeamCoaches.Where(tc => tc.UserId != null && ids.Contains(tc.UserId)).Select(tc => tc.UserId!).ToListAsync(ct))
            .Concat(await _db.Coaches.Where(c => c.UserId != null && ids.Contains(c.UserId)).Select(c => c.UserId!).ToListAsync(ct))
            .ToHashSet();
        var installs = await _db.MobileAppInstalls.Where(i => ids.Contains(i.UserId)).Select(i => new { i.UserId, i.LastSeenAt }).ToListAsync(ct);
        var devices = await _db.DeviceTokens.Where(d => ids.Contains(d.UserId)).Select(d => new { d.UserId, d.LastSeenAt }).ToListAsync(ct);
        var onApp = installs.Select(i => i.UserId).Concat(devices.Select(d => d.UserId)).ToHashSet();
        var staffRole = members.Where(m => m.UserId != null && m.Role == ChatMemberRole.Admin).Select(m => m.UserId!).ToHashSet();

        // Most recent sign of life per login, to pick which of a person's logins to show.
        var lastActive = (await _db.Users.Where(u => ids.Contains(u.Id)).Select(u => new { u.Id, u.LastLoginAt }).ToListAsync(ct))
            .ToDictionary(u => u.Id, u => u.LastLoginAt ?? DateTime.MinValue);
        foreach (var a in installs.Select(i => (i.UserId, i.LastSeenAt)).Concat(devices.Select(d => (d.UserId, d.LastSeenAt))))
            if (!lastActive.TryGetValue(a.UserId, out var cur) || a.LastSeenAt > cur) lastActive[a.UserId] = a.LastSeenAt;

        // Same family + same name or phone = the same parent on two logins.
        static string Norm(string s) => string.Join(' ', s.ToLowerInvariant().Split(' ', StringSplitOptions.RemoveEmptyEntries));
        static string? Digits(string? phone)
        {
            var d = new string((phone ?? string.Empty).Where(char.IsDigit).ToArray());
            return d.Length >= 10 ? d[^10..] : null;
        }
        bool SamePerson(string a, string b) =>
            Norm(people[a].Name) == Norm(people[b].Name)
            || (Digits(phoneOf.GetValueOrDefault(a)) is string pa && pa == Digits(phoneOf.GetValueOrDefault(b)));

        var ownerIds = families.Select(f => f.UserId).ToHashSet();
        var shownAs = new Dictionary<string, string>();
        foreach (var family in familyOf.GroupBy(kv => kv.Value))
        {
            var clusters = new List<List<string>>();
            foreach (var id in family.Select(kv => kv.Key))
            {
                var cluster = clusters.FirstOrDefault(cl => cl.Any(other => SamePerson(other, id)));
                if (cluster is null) clusters.Add(new List<string> { id });
                else cluster.Add(id);
            }
            foreach (var cluster in clusters.Where(cl => cl.Count > 1))
            {
                var shown = cluster.Contains(viewerUserId)
                    ? viewerUserId
                    : cluster.OrderByDescending(id => lastActive.GetValueOrDefault(id)).First();
                foreach (var id in cluster) shownAs[id] = shown;
            }
        }

        var result = new List<MobileChatPersonDto>();
        foreach (var (id, p) in people)
        {
            if (shownAs.TryGetValue(id, out var shown) && shown != id) continue; // shown as another login
            var logins = shownAs.Where(kv => kv.Value == id).Select(kv => kv.Key).DefaultIfEmpty(id).ToList();
            // A merged person keeps the family's own spelling of their name when the owner login is one of them.
            var name = logins.Where(ownerIds.Contains).Select(l => people[l].Name).FirstOrDefault() ?? p.Name;
            result.Add(new MobileChatPersonDto(
                id, name, p.Detail,
                IsAdmin: logins.Any(l => adminIds.Contains(l) || staffRole.Contains(l)),
                IsCoach: logins.Any(coachIds.Contains),
                OnApp: logins.Any(onApp.Contains),
                IsYou: logins.Contains(viewerUserId)));
        }
        var ordered = result
            .OrderBy(p => p.IsAdmin || p.IsCoach ? 0 : 1)
            .ThenBy(p => p.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
        return (ordered, shownAs);
    }

    public async Task<DirectChatOutcome> OpenDirectAsync(string callerUserId, string targetUserId, int viaGroupId, CancellationToken ct)
    {
        if (callerUserId == targetUserId) return new DirectChatOutcome(null, "That's you.");
        if (!await IsMemberAsync(viaGroupId, callerUserId, ct)) return new DirectChatOutcome(null, Forbidden: true);
        var (people, shownAs) = await LoadPeopleAsync(viaGroupId, callerUserId, ct);
        // A parent with two linked logins is one person: message the login they use.
        targetUserId = shownAs.GetValueOrDefault(targetUserId, targetUserId);
        if (targetUserId == callerUserId) return new DirectChatOutcome(null, "That's you.");
        var them = people.FirstOrDefault(p => p.UserId == targetUserId);
        if (them is null) return new DirectChatOutcome(null, Forbidden: true);
        var me = people.FirstOrDefault(p => p.IsYou);

        var blocked = await _db.ChatUserBlocks.AnyAsync(b =>
            (b.BlockerUserId == callerUserId && b.BlockedUserId == targetUserId) ||
            (b.BlockerUserId == targetUserId && b.BlockedUserId == callerUserId), ct);
        if (blocked) return new DirectChatOutcome(null, "You can't message this person.");

        var key = string.CompareOrdinal(callerUserId, targetUserId) < 0
            ? $"{callerUserId}|{targetUserId}"
            : $"{targetUserId}|{callerUserId}";
        var existing = await _db.ChatGroups.Where(g => g.DirectKey == key).Select(g => (int?)g.Id).FirstOrDefaultAsync(ct);
        if (existing is int id) return new DirectChatOutcome(id);

        var myName = me?.Name ?? "Parent";
        var title = $"{myName} & {them.Name}";
        var chat = new ChatGroup
        {
            IsDirect = true,
            DirectKey = key,
            Title = title.Length > 128 ? title[..128] : title,
            Members =
            {
                new ChatGroupMember { UserId = callerUserId, DisplayName = myName, Role = me?.IsAdmin == true ? ChatMemberRole.Admin : ChatMemberRole.Parent },
                new ChatGroupMember { UserId = targetUserId, DisplayName = them.Name, Role = them.IsAdmin ? ChatMemberRole.Admin : ChatMemberRole.Parent },
            },
        };
        _db.ChatGroups.Add(chat);
        try
        {
            await _db.SaveChangesAsync(ct);
            return new DirectChatOutcome(chat.Id);
        }
        catch (DbUpdateException)
        {
            // The other person opened the same chat at the same moment: use theirs.
            _db.ChangeTracker.Clear();
            var raced = await _db.ChatGroups.Where(g => g.DirectKey == key).Select(g => (int?)g.Id).FirstOrDefaultAsync(ct);
            return raced is int r ? new DirectChatOutcome(r) : throw new InvalidOperationException("Could not open the chat.");
        }
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

        var archived = await FamilyArchive.ArchivedIdsAsync(_db, ct);
        var parentAccountIds = members.Where(m => m.ParentAccountId != null).Select(m => m.ParentAccountId!.Value)
            .Where(id => !archived.Contains(id)).ToList();
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
