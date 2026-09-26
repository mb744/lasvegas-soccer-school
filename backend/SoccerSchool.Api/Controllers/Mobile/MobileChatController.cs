using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SoccerSchool.Api.Data;
using SoccerSchool.Api.Domain;
using SoccerSchool.Api.Dtos;
using SoccerSchool.Api.Services;

namespace SoccerSchool.Api.Controllers.Mobile;

/// <summary>
/// REST surface for chat: the caller's groups (with unread counts), message history with
/// pagination, a send fallback, and read-marking. The live path is the SignalR <c>ChatHub</c>;
/// this exists for initial load, scroll-back, and clients that drop the socket.
/// </summary>
[ApiController]
[Route("api/mobile/chat")]
[Authorize(AuthenticationSchemes = AuthSchemes.MobileJwt)]
public class MobileChatController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IChatService _chat;
    private readonly IParentAccountResolver _accounts;
    private readonly UserManager<ApplicationUser> _users;

    public MobileChatController(
        AppDbContext db, IChatService chat, IParentAccountResolver accounts, UserManager<ApplicationUser> users)
    {
        _db = db;
        _chat = chat;
        _accounts = accounts;
        _users = users;
    }

    [HttpGet("groups")]
    public async Task<ActionResult<IEnumerable<MobileChatGroupDto>>> Groups(CancellationToken ct)
    {
        var userId = _users.GetUserId(User);
        if (string.IsNullOrEmpty(userId)) return Unauthorized();
        var account = await _accounts.ResolveGuardianByUserIdAsync(userId, ct);
        var accountId = account?.Id;

        var members = await _db.ChatGroupMembers
            .Where(m => (accountId != null && m.ParentAccountId == accountId) || m.UserId == userId)
            .Select(m => new { m.ChatGroupId, m.LastReadMessageId })
            .ToListAsync(ct);
        if (members.Count == 0) return Ok(Array.Empty<MobileChatGroupDto>());

        var groupIds = members.Select(m => m.ChatGroupId).ToList();
        var lastReadByGroup = members
            .GroupBy(m => m.ChatGroupId)
            .ToDictionary(g => g.Key, g => g.Max(m => m.LastReadMessageId ?? 0));

        var groups = await _db.ChatGroups
            .Where(g => groupIds.Contains(g.Id))
            .Select(g => new
            {
                g.Id, g.Title,
                Last = g.Messages.OrderByDescending(m => m.SentAt)
                    .Select(m => new
                    {
                        m.Id, m.Body, m.SenderName, m.SentAt,
                        MediaKind = m.MediaAsset != null ? (MediaKind?)m.MediaAsset.Kind : null,
                    })
                    .FirstOrDefault(),
            })
            .ToListAsync(ct);

        var spanish = account?.Language == Language.Spanish;
        string? Preview(string? body, MediaKind? kind) =>
            !string.IsNullOrWhiteSpace(body) || kind is null ? body
            : kind == MediaKind.Video ? "🎥 Video"
            : spanish ? "📷 Foto" : "📷 Photo";

        // Unread = messages newer (higher id) than this member's last-read marker.
        var unreadCounts = await _db.ChatMessages
            .Where(m => groupIds.Contains(m.ChatGroupId))
            .GroupBy(m => m.ChatGroupId)
            .Select(g => new { GroupId = g.Key, Ids = g.Select(m => m.Id).ToList() })
            .ToListAsync(ct);
        var unreadByGroup = unreadCounts.ToDictionary(
            x => x.GroupId,
            x => x.Ids.Count(id => id > (lastReadByGroup.TryGetValue(x.GroupId, out var lr) ? lr : 0)));

        var result = groups
            .Select(g => new MobileChatGroupDto(
                g.Id, g.Title,
                Preview(g.Last?.Body, g.Last?.MediaKind),
                g.Last?.SenderName,
                g.Last?.SentAt,
                unreadByGroup.TryGetValue(g.Id, out var u) ? u : 0))
            .OrderByDescending(g => g.LastMessageAt ?? DateTime.MinValue)
            .ToList();

        return Ok(result);
    }

    /// <summary>Message history for a group, newest-first, paginated with <c>before</c> (a message id;
    /// returns messages with a smaller id). Up to 50 per page. Messages authored by any user the
    /// caller has blocked are filtered out server-side.</summary>
    [HttpGet("groups/{groupId:int}/messages")]
    public async Task<ActionResult<IEnumerable<MobileChatMessageDto>>> Messages(
        int groupId, CancellationToken ct, [FromQuery] int? before = null, [FromQuery] int limit = 50)
    {
        var userId = _users.GetUserId(User);
        if (string.IsNullOrEmpty(userId)) return Unauthorized();
        if (!await _chat.IsMemberAsync(groupId, userId, ct)) return Forbid();

        limit = Math.Clamp(limit, 1, 100);
        var blocked = await _db.ChatUserBlocks
            .Where(x => x.BlockerUserId == userId)
            .Select(x => x.BlockedUserId)
            .ToListAsync(ct);

        var q = _db.ChatMessages.Where(m => m.ChatGroupId == groupId);
        if (before is int b) q = q.Where(m => m.Id < b);
        if (blocked.Count > 0) q = q.Where(m => !blocked.Contains(m.SenderUserId));

        var rows = await q
            .OrderByDescending(m => m.Id)
            .Take(limit)
            .Select(m => new
            {
                m.Id, m.ChatGroupId, m.SenderUserId, m.SenderName, m.IsFromAdmin, m.Body, m.SentAt,
                m.MediaAssetId,
                MediaKind = m.MediaAsset != null ? (MediaKind?)m.MediaAsset.Kind : null,
                MediaContentType = m.MediaAsset != null ? m.MediaAsset.ContentType : null,
                MediaBlobName = m.MediaAsset != null ? m.MediaAsset.BlobName : null,
            })
            .ToListAsync(ct);

        return Ok(rows.Select(m => new MobileChatMessageDto(
            m.Id, m.ChatGroupId, m.SenderUserId, m.SenderName, m.IsFromAdmin, m.Body, m.SentAt,
            _chat.ToMediaDto(m.MediaAssetId, m.MediaKind, m.MediaContentType, m.MediaBlobName))));
    }

    /// <summary>Flag a chat message for admin review (App Store Guideline 1.2). Any group member
    /// may report any message; multiple reports on the same message stack in the admin queue.</summary>
    [HttpPost("messages/{messageId:int}/report")]
    public async Task<IActionResult> Report(int messageId, [FromBody] MobileReportMessageRequest req, CancellationToken ct)
    {
        var userId = _users.GetUserId(User);
        if (string.IsNullOrEmpty(userId)) return Unauthorized();

        var message = await _db.ChatMessages.FirstOrDefaultAsync(m => m.Id == messageId, ct);
        if (message is null) return NotFound();
        if (!await _chat.IsMemberAsync(message.ChatGroupId, userId, ct)) return Forbid();

        var account = await _accounts.ResolveGuardianByUserIdAsync(userId, ct);
        var reporterName = account is null
            ? (await _users.FindByIdAsync(userId))?.Email ?? "Unknown"
            : $"{account.FirstName} {account.LastName}".Trim();

        _db.ChatMessageReports.Add(new ChatMessageReport
        {
            ChatMessageId = messageId,
            ReporterUserId = userId,
            ReporterName = reporterName,
            Reason = string.IsNullOrWhiteSpace(req.Reason) ? null : req.Reason.Trim(),
            ReportedAt = DateTime.UtcNow,
        });
        await _db.SaveChangesAsync(ct);
        return NoContent();
    }

    /// <summary>Return the user ids the caller has muted; the client applies this filter to live
    /// SignalR messages so a blocked sender never surfaces even before the next REST refresh.</summary>
    [HttpGet("blocks")]
    public async Task<ActionResult<IEnumerable<MobileBlockedUserDto>>> Blocks(CancellationToken ct)
    {
        var userId = _users.GetUserId(User);
        if (string.IsNullOrEmpty(userId)) return Unauthorized();

        var rows = await _db.ChatUserBlocks
            .Where(x => x.BlockerUserId == userId)
            .OrderByDescending(x => x.BlockedAt)
            .Select(x => new MobileBlockedUserDto(x.BlockedUserId, x.BlockedAt))
            .ToListAsync(ct);
        return Ok(rows);
    }

    /// <summary>Mute a user — their messages disappear from history and the live stream. Idempotent.</summary>
    [HttpPost("blocks/{targetUserId}")]
    public async Task<IActionResult> Block(string targetUserId, CancellationToken ct)
    {
        var userId = _users.GetUserId(User);
        if (string.IsNullOrEmpty(userId)) return Unauthorized();
        if (string.IsNullOrWhiteSpace(targetUserId) || targetUserId == userId) return BadRequest();

        var existing = await _db.ChatUserBlocks
            .FirstOrDefaultAsync(x => x.BlockerUserId == userId && x.BlockedUserId == targetUserId, ct);
        if (existing is null)
        {
            _db.ChatUserBlocks.Add(new ChatUserBlock
            {
                BlockerUserId = userId,
                BlockedUserId = targetUserId,
                BlockedAt = DateTime.UtcNow,
            });
            await _db.SaveChangesAsync(ct);
        }
        return NoContent();
    }

    /// <summary>Unblock — the muted user's messages become visible again on next refresh.</summary>
    [HttpDelete("blocks/{targetUserId}")]
    public async Task<IActionResult> Unblock(string targetUserId, CancellationToken ct)
    {
        var userId = _users.GetUserId(User);
        if (string.IsNullOrEmpty(userId)) return Unauthorized();

        var existing = await _db.ChatUserBlocks
            .FirstOrDefaultAsync(x => x.BlockerUserId == userId && x.BlockedUserId == targetUserId, ct);
        if (existing is not null)
        {
            _db.ChatUserBlocks.Remove(existing);
            await _db.SaveChangesAsync(ct);
        }
        return NoContent();
    }

    /// <summary>Send via REST (fallback when the websocket is down). Goes through the same path as
    /// the hub, so it persists, fans out to connected clients, and pushes offline members.</summary>
    [HttpPost("groups/{groupId:int}/messages")]
    public async Task<ActionResult<MobileChatMessageDto>> Send(
        int groupId, [FromBody] MobileSendMessageRequest req, CancellationToken ct)
    {
        var userId = _users.GetUserId(User);
        if (string.IsNullOrEmpty(userId)) return Unauthorized();
        if (string.IsNullOrWhiteSpace(req.Body) && req.MediaId is null)
            return BadRequest("Message body or attachment is required.");

        MediaAsset? media = null;
        if (req.MediaId is int mediaId)
        {
            // Only your own, server-verified upload can be attached.
            media = await _db.MediaAssets.FirstOrDefaultAsync(
                a => a.Id == mediaId && a.UploadedByUserId == userId && a.Status == MediaStatus.Ready, ct);
            if (media is null) return BadRequest("Attachment not found or not finished uploading.");
        }

        var dto = await _chat.PostMessageAsync(groupId, userId, req.Body ?? "", ct, media: media);
        if (dto is null) return Forbid();
        return Ok(dto);
    }

    /// <summary>Marks the group read up to a message id (drives the unread badge).</summary>
    [HttpPost("groups/{groupId:int}/read")]
    public async Task<IActionResult> MarkRead(int groupId, [FromQuery] int messageId, CancellationToken ct)
    {
        var userId = _users.GetUserId(User);
        if (string.IsNullOrEmpty(userId)) return Unauthorized();
        var account = await _accounts.ResolveGuardianByUserIdAsync(userId, ct);
        var accountId = account?.Id;

        var member = await _db.ChatGroupMembers.FirstOrDefaultAsync(
            m => m.ChatGroupId == groupId &&
                 ((accountId != null && m.ParentAccountId == accountId) || m.UserId == userId), ct);
        if (member is null) return Forbid();

        if (messageId > (member.LastReadMessageId ?? 0))
        {
            member.LastReadMessageId = messageId;
            await _db.SaveChangesAsync(ct);
        }
        return NoContent();
    }
}
