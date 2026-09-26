using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SoccerSchool.Api.Data;
using SoccerSchool.Api.Domain;
using SoccerSchool.Api.Services;

namespace SoccerSchool.Api.Controllers.Mobile;

/// <summary>
/// Staff-scoped (admin OR coach) read helpers on top of a specific event. The parent-facing
/// mobile schedule endpoint returns only the caller's own kids; this one exposes the team-wide
/// attendance count so the coach or admin can see how many players are Going / Maybe / Not going
/// / Pending on a card. Requires the caller to either be an admin or the coach of the event's
/// team — non-staff parents get a 403.
/// </summary>
[ApiController]
[Route("api/mobile/staff-events")]
[Authorize(AuthenticationSchemes = AuthSchemes.CookieOrMobileJwt)]
public class MobileStaffEventController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly UserManager<ApplicationUser> _users;
    private readonly ICoachScopeService _coaches;

    public MobileStaffEventController(AppDbContext db, UserManager<ApplicationUser> users, ICoachScopeService coaches)
    {
        _db = db;
        _users = users;
        _coaches = coaches;
    }

    [HttpGet("{id:int}/attendance")]
    public async Task<ActionResult<MobileStaffAttendanceDto>> Attendance(int id, CancellationToken ct)
    {
        var user = await _users.GetUserAsync(User);
        if (user is null) return Unauthorized();

        var ev = await _db.ScheduledGames
            .Where(g => g.Id == id)
            .Select(g => new { g.Id, g.TeamId })
            .FirstOrDefaultAsync(ct);
        if (ev is null) return NotFound();

        // Staff = site admin OR the coach of this event's team (coach card linked to this login;
        // never a bare email match, since sign-up doesn't verify email).
        var scope = await _coaches.GetScopeAsync(User, ct);
        if (!scope.CanManageTeam(ev.TeamId)) return Forbid();

        var rosterPlayerIds = await _db.TeamPlayers
            .Where(tp => tp.TeamId == ev.TeamId)
            .Select(tp => tp.PlayerId)
            .ToListAsync(ct);

        var attendanceByPlayer = await _db.EventAttendances
            .Where(a => a.ScheduledGameId == ev.Id && rosterPlayerIds.Contains(a.PlayerId))
            .Select(a => new { a.PlayerId, a.Status })
            .ToListAsync(ct);
        var statusByPlayer = attendanceByPlayer.ToDictionary(a => a.PlayerId, a => a.Status);

        int going = 0, maybe = 0, notGoing = 0, pending = 0;
        foreach (var pid in rosterPlayerIds)
        {
            var s = statusByPlayer.TryGetValue(pid, out var st) ? st : AttendanceStatus.Pending;
            switch (s)
            {
                case AttendanceStatus.Confirmed: going++; break;
                case AttendanceStatus.Maybe: maybe++; break;
                case AttendanceStatus.Declined: notGoing++; break;
                default: pending++; break;
            }
        }

        return Ok(new MobileStaffAttendanceDto(going, maybe, notGoing, pending, rosterPlayerIds.Count));
    }
}

public record MobileStaffAttendanceDto(int Going, int Maybe, int NotGoing, int Pending, int RosterSize);
