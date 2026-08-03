using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SoccerSchool.Api.Data;
using SoccerSchool.Api.Domain;
using SoccerSchool.Api.Dtos;
using SoccerSchool.Api.Services;

namespace SoccerSchool.Api.Controllers.Mobile;

/// <summary>
/// Parent-facing schedule for the mobile app: every game/practice/event for the teams the caller's
/// kids are rostered on, plus those kids' attendance status per event. Read-only here — confirming
/// attendance is <see cref="MobileAttendanceController"/>. Scoped to the caller's family via
/// <see cref="IParentAccountResolver"/>, so a parent only ever sees their own children's teams.
/// </summary>
[ApiController]
[Route("api/mobile/schedule")]
[Authorize(AuthenticationSchemes = AuthSchemes.MobileJwt)]
public class MobileScheduleController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IParentAccountResolver _accounts;

    public MobileScheduleController(AppDbContext db, IParentAccountResolver accounts)
    {
        _db = db;
        _accounts = accounts;
    }

    /// <summary>Upcoming (and recent) events across all the caller's kids' teams. Defaults to the
    /// window [yesterday, +60 days]; override with <c>from</c>/<c>to</c> (UTC).</summary>
    [HttpGet]
    public async Task<ActionResult<IEnumerable<MobileScheduleEventDto>>> List(
        CancellationToken ct, [FromQuery] DateTime? from = null, [FromQuery] DateTime? to = null)
    {
        var account = await _accounts.ResolveAsync(User, ct);
        if (account is null) return Unauthorized();

        var fromUtc = (from ?? DateTime.UtcNow.AddDays(-1)).ToUniversalTime();
        var toUtc = (to ?? DateTime.UtcNow.AddDays(60)).ToUniversalTime();

        // The caller's kids and the teams they're on.
        var myPlayerIds = await _db.Players
            .Where(p => p.ParentAccountId == account.Id)
            .Select(p => p.Id)
            .ToListAsync(ct);
        if (myPlayerIds.Count == 0) return Ok(Array.Empty<MobileScheduleEventDto>());

        var teamIds = await _db.TeamPlayers
            .Where(tp => myPlayerIds.Contains(tp.PlayerId))
            .Select(tp => tp.TeamId)
            .Distinct()
            .ToListAsync(ct);
        if (teamIds.Count == 0) return Ok(Array.Empty<MobileScheduleEventDto>());

        var events = await _db.ScheduledGames
            .Where(g => teamIds.Contains(g.TeamId) && g.StartsAt >= fromUtc && g.StartsAt <= toUtc)
            .OrderBy(g => g.StartsAt)
            .Select(g => new
            {
                g.Id, g.TeamId, TeamName = g.Team!.Name, g.Kind, g.StartsAt, g.EndsAt, g.ArriveAt,
                g.Summary, g.Location, VenueName = g.Venue != null ? g.Venue.Name : null,
                g.OpponentName, g.IsHome, g.IsCancelled,
                UniformName = g.Uniform != null ? g.Uniform.Name : null, g.ShoeType,
            })
            .ToListAsync(ct);
        if (events.Count == 0) return Ok(Array.Empty<MobileScheduleEventDto>());

        // Which of the caller's kids are on each team (so each event lists only this family's players).
        var rosterByTeam = (await _db.TeamPlayers
            .Where(tp => teamIds.Contains(tp.TeamId) && myPlayerIds.Contains(tp.PlayerId))
            .Select(tp => new { tp.TeamId, tp.PlayerId, tp.Player!.FirstName, tp.Player.LastName })
            .ToListAsync(ct))
            .GroupBy(x => x.TeamId)
            .ToDictionary(g => g.Key, g => g.ToList());

        var eventIds = events.Select(e => e.Id).ToList();
        var attendance = await _db.EventAttendances
            .Where(a => eventIds.Contains(a.ScheduledGameId) && myPlayerIds.Contains(a.PlayerId))
            .ToDictionaryAsync(a => (a.ScheduledGameId, a.PlayerId), a => a.Status, ct);

        var result = events.Select(e =>
        {
            rosterByTeam.TryGetValue(e.TeamId, out var roster);
            var players = (roster ?? new())
                .OrderBy(r => r.FirstName).ThenBy(r => r.LastName)
                .Select(r => new MobileEventPlayerDto(
                    r.PlayerId, r.FirstName, r.LastName,
                    attendance.TryGetValue((e.Id, r.PlayerId), out var s) ? s : AttendanceStatus.Pending))
                .ToList();
            return new MobileScheduleEventDto(
                e.Id, e.TeamId, e.TeamName, e.Kind, e.StartsAt, e.EndsAt, e.ArriveAt,
                e.Summary, e.Location, e.VenueName, e.OpponentName, e.IsHome, e.IsCancelled,
                e.UniformName, e.ShoeType, players);
        }).ToList();

        return Ok(result);
    }
}
