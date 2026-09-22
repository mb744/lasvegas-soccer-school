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
/// Read-only feed of announcements a parent should see on the mobile Home tab. Returns every
/// currently-active school-wide announcement plus any active announcement targeted at a team one
/// of the parent's kids is on. Expired (past <see cref="Announcement.EndsAt"/>) and inactive
/// entries are filtered out server-side so the client can render the payload straight.
/// </summary>
[ApiController]
[Route("api/mobile/announcements")]
[Authorize(AuthenticationSchemes = AuthSchemes.MobileJwt)]
public class MobileAnnouncementsController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly UserManager<ApplicationUser> _users;
    private readonly IParentAccountResolver _accounts;

    public MobileAnnouncementsController(
        AppDbContext db, UserManager<ApplicationUser> users, IParentAccountResolver accounts)
    {
        _db = db;
        _users = users;
        _accounts = accounts;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<MobileAnnouncementDto>>> List(CancellationToken ct)
    {
        var userId = _users.GetUserId(User);
        if (string.IsNullOrEmpty(userId)) return Unauthorized();
        var account = await _accounts.ResolveByUserIdAsync(userId, ct);

        var teamIds = account is null
            ? new List<int>()
            : await _db.TeamPlayers
                .Where(tp => tp.Player!.ParentAccountId == account.Id)
                .Select(tp => tp.TeamId)
                .Distinct()
                .ToListAsync(ct);

        var now = DateTime.UtcNow;
        var rows = await _db.Announcements
            .Where(a => a.IsActive)
            .Where(a => a.EndsAt == null || a.EndsAt > now)
            .Where(a => a.TeamId == null || teamIds.Contains(a.TeamId.Value))
            .OrderByDescending(a => a.CreatedAt)
            .Select(a => new MobileAnnouncementDto(
                a.Id, a.Title, a.Body, a.TeamId,
                a.Team != null ? a.Team!.Name : null,
                a.CreatedAt))
            .ToListAsync(ct);
        return Ok(rows);
    }
}
