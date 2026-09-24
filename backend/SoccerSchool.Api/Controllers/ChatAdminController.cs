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
[Authorize(Roles = Roles.Admin, AuthenticationSchemes = AuthSchemes.CookieOrMobileJwt)]
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
        var coachEmails = await CoachEmailsAsync(ct);
        var groups = await _db.ChatGroups
            .OrderByDescending(g => g.CreatedAt)
            .Select(g => new
            {
                g.Id, g.Title, g.TeamId,
                TeamName = g.Team != null ? g.Team.Name : null,
                MemberCount = g.Members.Count, MessageCount = g.Messages.Count, g.CreatedAt,
                Members = g.Members.OrderBy(m => m.DisplayName)
                    .Select(m => new
                    {
                        m.Id, m.ParentAccountId, m.DisplayName, m.Role, m.AddedAt,
                        // Resolve the member's email: family membership → ParentAccount.User.Email;
                        // admin/staff membership (UserId set) → the ApplicationUser's Email.
                        MemberEmail = m.ParentAccountId != null && m.ParentAccount != null && m.ParentAccount!.User != null
                            ? m.ParentAccount!.User!.NormalizedEmail
                            : (m.UserId != null
                                ? _db.Users.Where(u => u.Id == m.UserId).Select(u => u.NormalizedEmail).FirstOrDefault()
                                : null),
                    }).ToList(),
            })
            .ToListAsync(ct);

        var result = groups.Select(g => new ChatGroupAdminDto(
            g.Id, g.Title, g.TeamId, g.TeamName, g.MemberCount, g.MessageCount, g.CreatedAt,
            g.Members.Select(m => new ChatGroupMemberDto(
                m.Id, m.ParentAccountId, m.DisplayName, m.Role,
                m.MemberEmail != null && coachEmails.Contains(m.MemberEmail),
                m.AddedAt)).ToList()));
        return Ok(result);
    }

    /// <summary>Every TeamCoach email in the DB, normalized (upper-invariant) to match Identity's
    /// NormalizedEmail column. Callers do a set-lookup to tag members as coaches.</summary>
    private async Task<HashSet<string>> CoachEmailsAsync(CancellationToken ct)
    {
        var rows = await _db.TeamCoaches
            .Where(tc => tc.Email != null && tc.Email != "")
            .Select(tc => tc.Email!)
            .ToListAsync(ct);
        return rows.Select(e => e.Trim().ToUpperInvariant()).ToHashSet(StringComparer.Ordinal);
    }

    [HttpPost]
    public async Task<ActionResult<ChatGroupAdminDto>> Create([FromBody] SaveChatGroupRequest req, CancellationToken ct)
    {
        var title = req.Title?.Trim();
        if (string.IsNullOrWhiteSpace(title)) return BadRequest("Title is required.");

        var group = new ChatGroup { Title = title!, TeamId = req.SeedFromTeamId };
        _db.ChatGroups.Add(group);
        await _db.SaveChangesAsync(ct);

        await AddCreatingAdminAsync(group.Id, ct);
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

    /// <summary>Parent picker for the chat-groups admin page. Matches on first name, last name, the
    /// full "First Last" concatenation, or email — case-insensitive substring. Unlike the messaging
    /// inbox picker, there is no phone-required filter and NoCommunications parents are included:
    /// chat is in-app, not SMS. Anonymized (deleted) accounts are filtered out.</summary>
    [HttpGet("search-parents")]
    public async Task<ActionResult<IEnumerable<ChatParentSearchDto>>> SearchParents(
        [FromQuery] string? q, [FromQuery] int limit = 20, CancellationToken ct = default)
    {
        var cap = Math.Clamp(limit, 1, 100);
        // Anonymized accounts get FirstName="Deleted", LastName="User" — hide them from the picker.
        var query = _db.ParentAccounts
            .Where(p => p.ReclaimEmailHash == null);

        if (!string.IsNullOrWhiteSpace(q))
        {
            var needle = q.Trim();
            query = query.Where(p =>
                EF.Functions.Like(p.FirstName, $"%{needle}%")
                || EF.Functions.Like(p.LastName, $"%{needle}%")
                || EF.Functions.Like(p.FirstName + " " + p.LastName, $"%{needle}%")
                || (p.User != null && p.User.Email != null && EF.Functions.Like(p.User.Email, $"%{needle}%")));
        }

        var rows = await query
            .OrderBy(p => p.LastName).ThenBy(p => p.FirstName)
            .Take(cap)
            .Select(p => new ChatParentSearchDto(
                p.Id,
                (p.FirstName + " " + p.LastName).Trim(),
                p.User != null ? p.User.Email : null,
                p.CellPhone))
            .ToListAsync(ct);
        return Ok(rows);
    }

    [HttpPost("{id:int}/members")]
    public async Task<ActionResult<ChatGroupAdminDto>> AddMember(int id, [FromBody] AddChatGroupMemberRequest req, CancellationToken ct)
    {
        var group = await _db.ChatGroups.FindAsync(new object?[] { id }, ct);
        if (group is null) return NotFound();

        var account = await _db.ParentAccounts.FirstOrDefaultAsync(a => a.Id == req.ParentAccountId, ct);
        if (account is null) return BadRequest("Parent account not found.");

        await AddFamilyMembersAsync(id, new[] { new FamilyRosterEntry(account.Id, account.FirstName, account.LastName) }, ct);
        await _db.SaveChangesAsync(ct);
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

    /// <summary>Adds the admin who created the group as an admin member so they show up in the
    /// roster and their SignalR sends resolve to their own row (with an Admin role tag). No-op if
    /// they're already a member (e.g. re-run against an existing group).</summary>
    private async Task AddCreatingAdminAsync(int groupId, CancellationToken ct)
    {
        var userId = _users.GetUserId(User);
        if (string.IsNullOrEmpty(userId)) return;

        var already = await _db.ChatGroupMembers.AnyAsync(
            m => m.ChatGroupId == groupId && m.UserId == userId, ct);
        if (already) return;

        var name = await _db.ParentAccounts
            .Where(a => a.UserId == userId)
            .Select(a => (a.FirstName + " " + a.LastName).Trim())
            .FirstOrDefaultAsync(ct);
        if (string.IsNullOrWhiteSpace(name))
        {
            var user = await _users.FindByIdAsync(userId);
            name = user?.Email ?? "Coach";
        }

        _db.ChatGroupMembers.Add(new ChatGroupMember
        {
            ChatGroupId = groupId,
            UserId = userId,
            DisplayName = name!,
            Role = ChatMemberRole.Admin,
        });
        await _db.SaveChangesAsync(ct);
    }

    private record FamilyRosterEntry(int ParentAccountId, string FirstName, string LastName);

    /// <summary>Adds every parent linked to a team's roster as a group member (deduped). For each
    /// player, both the primary parent (via ParentAccount) AND every linked collaborator (mom + dad
    /// both signed up) are added. Owner is a family-linked row; each collaborator is a user-linked
    /// row with their own display name so their chat sends carry the right sender label. Also adds
    /// every <see cref="TeamCoach"/> whose email matches an existing ApplicationUser as an admin
    /// member — coaches without an account are skipped (send them an invite from the Coaches page).</summary>
    private async Task SeedFromTeamAsync(int groupId, int teamId, CancellationToken ct)
    {
        var families = await _db.TeamPlayers
            .Where(tp => tp.TeamId == teamId)
            .Select(tp => new FamilyRosterEntry(
                tp.Player!.ParentAccountId,
                tp.Player.ParentAccount!.FirstName,
                tp.Player.ParentAccount.LastName))
            .Distinct()
            .ToListAsync(ct);

        if (families.Count > 0)
            await AddFamilyMembersAsync(groupId, families, ct);

        await AddTeamCoachesAsync(groupId, teamId, ct);
        await _db.SaveChangesAsync(ct);
    }

    /// <summary>Adds each <see cref="TeamCoach"/> for the team as an Admin member of the group.
    /// Matches on email → ApplicationUser; coaches without an email or without a signed-up
    /// account are skipped (their message would have no <c>SenderUserId</c> to attribute to).
    /// Caller saves changes.</summary>
    private async Task AddTeamCoachesAsync(int groupId, int teamId, CancellationToken ct)
    {
        var coaches = await _db.TeamCoaches
            .Where(tc => tc.TeamId == teamId && tc.Email != null && tc.Email != "")
            .Select(tc => new { tc.Name, tc.Email })
            .ToListAsync(ct);
        if (coaches.Count == 0) return;

        // Normalize email lookups against Identity's NormalizedEmail column (uppercase invariant)
        // so a "Coach@Example.com" TeamCoach still matches "coach@example.com" on ApplicationUser.
        var normalized = coaches
            .Select(c => c.Email!.Trim().ToUpperInvariant())
            .Where(e => !string.IsNullOrEmpty(e))
            .Distinct()
            .ToList();
        var byNormalized = await _db.Users
            .Where(u => u.NormalizedEmail != null && normalized.Contains(u.NormalizedEmail))
            .Select(u => new { u.Id, u.NormalizedEmail })
            .ToListAsync(ct);
        var idByEmail = byNormalized.ToDictionary(x => x.NormalizedEmail!, x => x.Id);

        var existingUserIds = new HashSet<string>(
            await _db.ChatGroupMembers
                .Where(m => m.ChatGroupId == groupId && m.UserId != null)
                .Select(m => m.UserId!)
                .ToListAsync(ct));

        foreach (var tc in coaches)
        {
            var key = tc.Email!.Trim().ToUpperInvariant();
            if (!idByEmail.TryGetValue(key, out var userId)) continue;
            if (!existingUserIds.Add(userId)) continue;

            _db.ChatGroupMembers.Add(new ChatGroupMember
            {
                ChatGroupId = groupId,
                UserId = userId,
                DisplayName = string.IsNullOrWhiteSpace(tc.Name) ? "Coach" : tc.Name.Trim(),
                Role = ChatMemberRole.Admin,
            });
        }
    }

    /// <summary>Adds one family-linked member row per family plus one user-linked row for each of
    /// the family's <see cref="ParentAccountCollaborator"/> logins. Idempotent — existing rows for
    /// the same (group, family) or (group, user) pair are skipped. Caller saves changes.</summary>
    private async Task AddFamilyMembersAsync(int groupId, IReadOnlyCollection<FamilyRosterEntry> families, CancellationToken ct)
    {
        if (families.Count == 0) return;
        var familyIds = families.Select(f => f.ParentAccountId).Distinct().ToList();

        var existingFamilyIds = new HashSet<int>(
            await _db.ChatGroupMembers
                .Where(m => m.ChatGroupId == groupId && m.ParentAccountId != null)
                .Select(m => m.ParentAccountId!.Value)
                .ToListAsync(ct));
        var existingUserIds = new HashSet<string>(
            await _db.ChatGroupMembers
                .Where(m => m.ChatGroupId == groupId && m.UserId != null)
                .Select(m => m.UserId!)
                .ToListAsync(ct));

        // Family (owner) rows. The family row implicitly covers the ParentAccount.UserId login;
        // collaborator logins are added separately below so each one has its own display name.
        foreach (var f in families)
        {
            if (!existingFamilyIds.Add(f.ParentAccountId)) continue;
            _db.ChatGroupMembers.Add(new ChatGroupMember
            {
                ChatGroupId = groupId,
                ParentAccountId = f.ParentAccountId,
                DisplayName = $"{f.FirstName} {f.LastName}".Trim(),
                Role = ChatMemberRole.Parent,
            });
        }

        // Collaborator rows. Look up each collaborator's own ParentAccount so their display name
        // is their own, not the owning family's. Fall back to email when they have no account yet.
        var collaborators = await _db.ParentAccountCollaborators
            .Where(c => familyIds.Contains(c.ParentAccountId))
            .Select(c => new
            {
                c.UserId,
                OwnFirst = _db.ParentAccounts.Where(p => p.UserId == c.UserId).Select(p => p.FirstName).FirstOrDefault(),
                OwnLast = _db.ParentAccounts.Where(p => p.UserId == c.UserId).Select(p => p.LastName).FirstOrDefault(),
                Email = c.User != null ? c.User.Email : null,
            })
            .ToListAsync(ct);

        foreach (var c in collaborators)
        {
            if (string.IsNullOrEmpty(c.UserId) || !existingUserIds.Add(c.UserId)) continue;
            var name = !string.IsNullOrWhiteSpace(c.OwnFirst)
                ? $"{c.OwnFirst} {c.OwnLast}".Trim()
                : (c.Email ?? "Parent");
            _db.ChatGroupMembers.Add(new ChatGroupMember
            {
                ChatGroupId = groupId,
                UserId = c.UserId,
                DisplayName = name,
                Role = ChatMemberRole.Parent,
            });
        }
    }

    private async Task<ChatGroupAdminDto> SummarizeAsync(int id, CancellationToken ct)
    {
        var coachEmails = await CoachEmailsAsync(ct);
        var g = await _db.ChatGroups
            .Where(x => x.Id == id)
            .Select(x => new
            {
                x.Id, x.Title, x.TeamId,
                TeamName = x.Team != null ? x.Team.Name : null,
                MemberCount = x.Members.Count, MessageCount = x.Messages.Count, x.CreatedAt,
                Members = x.Members.OrderBy(m => m.DisplayName)
                    .Select(m => new
                    {
                        m.Id, m.ParentAccountId, m.DisplayName, m.Role, m.AddedAt,
                        MemberEmail = m.ParentAccountId != null && m.ParentAccount != null && m.ParentAccount!.User != null
                            ? m.ParentAccount!.User!.NormalizedEmail
                            : (m.UserId != null
                                ? _db.Users.Where(u => u.Id == m.UserId).Select(u => u.NormalizedEmail).FirstOrDefault()
                                : null),
                    }).ToList(),
            })
            .FirstAsync(ct);

        return new ChatGroupAdminDto(
            g.Id, g.Title, g.TeamId, g.TeamName, g.MemberCount, g.MessageCount, g.CreatedAt,
            g.Members.Select(m => new ChatGroupMemberDto(
                m.Id, m.ParentAccountId, m.DisplayName, m.Role,
                m.MemberEmail != null && coachEmails.Contains(m.MemberEmail),
                m.AddedAt)).ToList());
    }
}
