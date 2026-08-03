using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SoccerSchool.Api.Data;
using SoccerSchool.Api.Domain;
using SoccerSchool.Api.Dtos;
using SoccerSchool.Api.Services;

namespace SoccerSchool.Api.Controllers;

/// <summary>
/// Admin (web) management of the native in-app chat groups parents use on mobile. Admins create a
/// group — optionally seeded from a team's roster — add/remove parent members, and can post into a
/// group (which fans out over SignalR + push exactly like a parent's message). Cookie-authenticated
/// like the rest of the admin surface; the parent side is the JWT-authed <c>MobileChatController</c>.
/// </summary>
[ApiController]
[Route("api/admin/chat-groups")]
[Authorize(Roles = Roles.Admin)]
public class ChatAdminController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IChatService _chat;
    private readonly UserManager<ApplicationUser> _users;

    public ChatAdminController(AppDbContext db, IChatService chat, UserManager<ApplicationUser> users)
    {
        _db = db;
        _chat = chat;
        _users = users;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<ChatGroupAdminDto>>> List(CancellationToken ct)
    {
        var rows = await _db.ChatGroups
            .OrderByDescending(g => g.CreatedAt)
            .Select(g => new ChatGroupAdminDto(
                g.Id, g.Title, g.TeamId, g.Team != null ? g.Team.Name : null,
                g.Members.Count, g.Messages.Count, g.CreatedAt,
                g.Members.OrderBy(m => m.DisplayName).Select(m => new ChatGroupMemberDto(
                    m.Id, m.ParentAccountId, m.DisplayName, m.Role, m.AddedAt)).ToList()))
            .ToListAsync(ct);
        return Ok(rows);
    }

    [HttpPost]
    public async Task<ActionResult<ChatGroupAdminDto>> Create([FromBody] SaveChatGroupRequest req, CancellationToken ct)
    {
        var title = req.Title?.Trim();
        if (string.IsNullOrWhiteSpace(title)) return BadRequest("Title is required.");

        var group = new ChatGroup { Title = title!, TeamId = req.SeedFromTeamId };
        _db.ChatGroups.Add(group);
        await _db.SaveChangesAsync(ct);

        if (req.SeedFromTeamId is int teamId)
            await SeedFromTeamAsync(group.Id, teamId, ct);

        return Ok(await SummarizeAsync(group.Id, ct));
    }

    [HttpPut("{id:int}")]
    public async Task<ActionResult<ChatGroupAdminDto>> Rename(int id, [FromBody] SaveChatGroupRequest req, CancellationToken ct)
    {
        var group = await _db.ChatGroups.FindAsync(new object?[] { id }, ct);
        if (group is null) return NotFound();
        var title = req.Title?.Trim();
        if (string.IsNullOrWhiteSpace(title)) return BadRequest("Title is required.");
        group.Title = title!;
        await _db.SaveChangesAsync(ct);
        return Ok(await SummarizeAsync(id, ct));
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        var group = await _db.ChatGroups.FindAsync(new object?[] { id }, ct);
        if (group is null) return NotFound();
        _db.ChatGroups.Remove(group);
        await _db.SaveChangesAsync(ct);
        return NoContent();
    }

    [HttpPost("{id:int}/members")]
    public async Task<ActionResult<ChatGroupAdminDto>> AddMember(int id, [FromBody] AddChatGroupMemberRequest req, CancellationToken ct)
    {
        var group = await _db.ChatGroups.FindAsync(new object?[] { id }, ct);
        if (group is null) return NotFound();

        var account = await _db.ParentAccounts.FirstOrDefaultAsync(a => a.Id == req.ParentAccountId, ct);
        if (account is null) return BadRequest("Parent account not found.");

        var already = await _db.ChatGroupMembers.AnyAsync(m => m.ChatGroupId == id && m.ParentAccountId == req.ParentAccountId, ct);
        if (!already)
        {
            _db.ChatGroupMembers.Add(new ChatGroupMember
            {
                ChatGroupId = id,
                ParentAccountId = account.Id,
                DisplayName = $"{account.FirstName} {account.LastName}".Trim(),
                Role = ChatMemberRole.Parent,
            });
            await _db.SaveChangesAsync(ct);
        }
        return Ok(await SummarizeAsync(id, ct));
    }

    [HttpDelete("{id:int}/members/{memberId:int}")]
    public async Task<ActionResult<ChatGroupAdminDto>> RemoveMember(int id, int memberId, CancellationToken ct)
    {
        var member = await _db.ChatGroupMembers.FirstOrDefaultAsync(m => m.Id == memberId && m.ChatGroupId == id, ct);
        if (member is null) return NotFound();
        _db.ChatGroupMembers.Remove(member);
        await _db.SaveChangesAsync(ct);
        return Ok(await SummarizeAsync(id, ct));
    }

    /// <summary>Admin posts into the group from the web — same fan-out path as a parent message.</summary>
    [HttpPost("{id:int}/messages")]
    public async Task<ActionResult<MobileChatMessageDto>> PostMessage(int id, [FromBody] MobileSendMessageRequest req, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.Body)) return BadRequest("Message body is required.");
        var group = await _db.ChatGroups.FindAsync(new object?[] { id }, ct);
        if (group is null) return NotFound();

        var userId = _users.GetUserId(User)!;
        var adminName = await _db.ParentAccounts
            .Where(a => a.UserId == userId)
            .Select(a => $"{a.FirstName} {a.LastName}".Trim())
            .FirstOrDefaultAsync(ct);
        if (string.IsNullOrWhiteSpace(adminName)) adminName = "Coach";

        var dto = await _chat.PostMessageAsync(id, userId, req.Body, ct, overrideName: adminName, asAdmin: true);
        return dto is null ? StatusCode(500, "Could not post message.") : Ok(dto);
    }

    /// <summary>Adds every parent on a team's roster as a group member (deduped). Used at create-time
    /// when <c>SeedFromTeamId</c> is set.</summary>
    private async Task SeedFromTeamAsync(int groupId, int teamId, CancellationToken ct)
    {
        var parents = await _db.TeamPlayers
            .Where(tp => tp.TeamId == teamId)
            .Select(tp => new
            {
                tp.Player!.ParentAccountId,
                tp.Player.ParentAccount!.FirstName,
                tp.Player.ParentAccount.LastName,
            })
            .Distinct()
            .ToListAsync(ct);

        var existing = await _db.ChatGroupMembers
            .Where(m => m.ChatGroupId == groupId && m.ParentAccountId != null)
            .Select(m => m.ParentAccountId!.Value)
            .ToListAsync(ct);
        var have = new HashSet<int>(existing);

        foreach (var p in parents)
        {
            if (!have.Add(p.ParentAccountId)) continue;
            _db.ChatGroupMembers.Add(new ChatGroupMember
            {
                ChatGroupId = groupId,
                ParentAccountId = p.ParentAccountId,
                DisplayName = $"{p.FirstName} {p.LastName}".Trim(),
                Role = ChatMemberRole.Parent,
            });
        }
        await _db.SaveChangesAsync(ct);
    }

    private async Task<ChatGroupAdminDto> SummarizeAsync(int id, CancellationToken ct)
    {
        return await _db.ChatGroups
            .Where(g => g.Id == id)
            .Select(g => new ChatGroupAdminDto(
                g.Id, g.Title, g.TeamId, g.Team != null ? g.Team.Name : null,
                g.Members.Count, g.Messages.Count, g.CreatedAt,
                g.Members.OrderBy(m => m.DisplayName).Select(m => new ChatGroupMemberDto(
                    m.Id, m.ParentAccountId, m.DisplayName, m.Role, m.AddedAt)).ToList()))
            .FirstAsync(ct);
    }
}
