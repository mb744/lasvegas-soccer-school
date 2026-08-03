using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SoccerSchool.Api.Data;
using SoccerSchool.Api.Domain;
using SoccerSchool.Api.Dtos;
using SoccerSchool.Api.Services;

namespace SoccerSchool.Api.Controllers.Mobile;

/// <summary>
/// Lets a parent confirm/decline a rostered child's attendance for an event from the app. Mirrors
/// the admin <c>ScheduleController.SetAttendance</c> upsert but is family-scoped and guarded: the
/// player must belong to the caller and be on the event's team, so a parent can never set another
/// family's player. Always stamped <see cref="AttendanceSource.ParentReply"/> — and we refuse to
/// overwrite a deliberate <see cref="AttendanceSource.Admin"/> call (same precedence the SMS-reply
/// parser respects).
/// </summary>
[ApiController]
[Route("api/mobile/events")]
[Authorize(AuthenticationSchemes = AuthSchemes.MobileJwt)]
public class MobileAttendanceController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IParentAccountResolver _accounts;

    public MobileAttendanceController(AppDbContext db, IParentAccountResolver accounts)
    {
        _db = db;
        _accounts = accounts;
    }

    [HttpPut("{eventId:int}/attendance")]
    public async Task<ActionResult<MobileEventPlayerDto>> SetAttendance(
        int eventId, [FromBody] MobileSetAttendanceRequest req, CancellationToken ct)
    {
        var account = await _accounts.ResolveAsync(User, ct);
        if (account is null) return Unauthorized();

        var ev = await _db.ScheduledGames.FirstOrDefaultAsync(g => g.Id == eventId, ct);
        if (ev is null) return NotFound("Event not found.");

        // Player must be the caller's child.
        var player = await _db.Players
            .FirstOrDefaultAsync(p => p.Id == req.PlayerId && p.ParentAccountId == account.Id, ct);
        if (player is null) return Forbid();

        // ...and on this event's team.
        var onRoster = await _db.TeamPlayers.AnyAsync(tp => tp.TeamId == ev.TeamId && tp.PlayerId == req.PlayerId, ct);
        if (!onRoster) return BadRequest("Player is not on this event's team.");

        var row = await _db.EventAttendances
            .FirstOrDefaultAsync(a => a.ScheduledGameId == eventId && a.PlayerId == req.PlayerId, ct);
        if (row is null)
        {
            row = new EventAttendance { ScheduledGameId = eventId, PlayerId = req.PlayerId };
            _db.EventAttendances.Add(row);
        }
        else if (row.Source == AttendanceSource.Admin)
        {
            // An admin deliberately set this; a parent tap shouldn't silently clobber it.
            return Conflict("Attendance for this player was set by a coach/admin. Please contact them to change it.");
        }

        row.Status = req.Status;
        row.Source = AttendanceSource.ParentReply;
        row.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        return Ok(new MobileEventPlayerDto(player.Id, player.FirstName, player.LastName, row.Status));
    }
}
