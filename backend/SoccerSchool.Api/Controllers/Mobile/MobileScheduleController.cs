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
                DirectUniform = g.Uniform,
                g.ShoeType,
            })
            .ToListAsync(ct);
        if (events.Count == 0) return Ok(Array.Empty<MobileScheduleEventDto>());

        // Fall back to the club-wide default kit when a game doesn't have its own uniform pin.
        // Mirrors ResolveEventUniformTextAsync in MessagingController so parents see the same
        // wear text ("white jersey, blue shorts, blue socks") on-app that the SMS/WhatsApp
        // reminders describe.
        var designatedUniforms = await _db.Uniforms.AsNoTracking()
            .Where(u => u.Designation != UniformDesignation.None)
            .ToListAsync(ct);
        var defaultsByDesignation = designatedUniforms
            .GroupBy(u => u.Designation)
            .ToDictionary(g => g.Key, g => g.First());
        Uniform? FallbackUniform(ScheduledEventKind kind, bool? isHome)
        {
            var designation = kind == ScheduledEventKind.Practice
                ? UniformDesignation.Practice
                : isHome switch
                {
                    true => UniformDesignation.Home,
                    false => UniformDesignation.Away,
                    _ => UniformDesignation.None,
                };
            return designation != UniformDesignation.None && defaultsByDesignation.TryGetValue(designation, out var u)
                ? u
                : null;
        }

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
            var uniform = e.DirectUniform ?? FallbackUniform(e.Kind, e.IsHome);
            // ToWearText yields "white jersey, blue shorts, blue socks" when colors are set, and
            // falls back to the uniform's own name when they aren't.
            var uniformText = uniform?.ToWearText();
            return new MobileScheduleEventDto(
                e.Id, e.TeamId, e.TeamName, e.Kind, e.StartsAt, e.EndsAt, e.ArriveAt,
                e.Summary, e.Location, e.VenueName, e.OpponentName, e.IsHome, e.IsCancelled,
                uniformText, e.ShoeType, players);
        }).ToList();

        return Ok(result);
    }
}
