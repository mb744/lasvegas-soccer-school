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
        var account = await _accounts.ResolveByUserIdAsync(userId, ct);
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
                    .Select(m => new { m.Id, m.Body, m.SenderName, m.SentAt })
                    .FirstOrDefault(),
            })
            .ToListAsync(ct);

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
                g.Last?.Body,
                g.Last?.SenderName,
                g.Last?.SentAt,
                unreadByGroup.TryGetValue(g.Id, out var u) ? u : 0))
            .OrderByDescending(g => g.LastMessageAt ?? DateTime.MinValue)
            .ToList();

        return Ok(result);
    }

    /// <summary>Message history for a group, newest-first, paginated with <c>before</c> (a message id;
    /// returns messages with a smaller id). Up to 50 per page.</summary>
    [HttpGet("groups/{groupId:int}/messages")]
    public async Task<ActionResult<IEnumerable<MobileChatMessageDto>>> Messages(
        int groupId, CancellationToken ct, [FromQuery] int? before = null, [FromQuery] int limit = 50)
    {
        var userId = _users.GetUserId(User);
        if (string.IsNullOrEmpty(userId)) return Unauthorized();
        if (!await _chat.IsMemberAsync(groupId, userId, ct)) return Forbid();

        limit = Math.Clamp(limit, 1, 100);
        var q = _db.ChatMessages.Where(m => m.ChatGroupId == groupId);
        if (before is int b) q = q.Where(m => m.Id < b);

        var rows = await q
            .OrderByDescending(m => m.Id)
            .Take(limit)
            .Select(m => new MobileChatMessageDto(
                m.Id, m.ChatGroupId, m.SenderUserId, m.SenderName, m.IsFromAdmin, m.Body, m.SentAt))
            .ToListAsync(ct);

        return Ok(rows);
    }

    /// <summary>Send via REST (fallback when the websocket is down). Goes through the same path as
    /// the hub, so it persists, fans out to connected clients, and pushes offline members.</summary>
    [HttpPost("groups/{groupId:int}/messages")]
    public async Task<ActionResult<MobileChatMessageDto>> Send(
        int groupId, [FromBody] MobileSendMessageRequest req, CancellationToken ct)
    {
        var userId = _users.GetUserId(User);
        if (string.IsNullOrEmpty(userId)) return Unauthorized();
        if (string.IsNullOrWhiteSpace(req.Body)) return BadRequest("Message body is required.");

        var dto = await _chat.PostMessageAsync(groupId, userId, req.Body, ct);
        if (dto is null) return Forbid();
        return Ok(dto);
    }

    /// <summary>Marks the group read up to a message id (drives the unread badge).</summary>
    [HttpPost("groups/{groupId:int}/read")]
    public async Task<IActionResult> MarkRead(int groupId, [FromQuery] int messageId, CancellationToken ct)
    {
        var userId = _users.GetUserId(User);
        if (string.IsNullOrEmpty(userId)) return Unauthorized();
        var account = await _accounts.ResolveByUserIdAsync(userId, ct);
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
