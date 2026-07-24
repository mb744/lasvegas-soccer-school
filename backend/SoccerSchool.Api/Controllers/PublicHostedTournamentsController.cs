using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SoccerSchool.Api.Data;
using SoccerSchool.Api.Domain;
using SoccerSchool.Api.Dtos;

namespace SoccerSchool.Api.Controllers;

/// <summary>
/// Anonymous read-only endpoint that powers the shareable /tournament/{slug} page. Returns
/// the event's headline info, rules body, day windows, fields, and scheduled matches — but
/// no admin-only fields (notes, per-team payment details, coach contact private info).
/// </summary>
[ApiController]
[Route("api/public/hosted-tournaments")]
[AllowAnonymous]
public class PublicHostedTournamentsController : ControllerBase
{
    private readonly AppDbContext _db;
    public PublicHostedTournamentsController(AppDbContext db) => _db = db;

    [HttpGet("{slug}")]
    public async Task<ActionResult<PublicScheduleDto>> Get(string slug, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(slug)) return NotFound();
        var s = slug.Trim().ToLowerInvariant();
        var t = await _db.HostedTournaments
            .AsNoTracking()
            .Include(x => x.Venue)
            .Include(x => x.Days)
            .Include(x => x.Fields).ThenInclude(f => f.VenueField)
            .Include(x => x.Tiers)
            .Include(x => x.Matches).ThenInclude(m => m.TeamA).ThenInclude(a => a!.LvssTeam)
            .Include(x => x.Matches).ThenInclude(m => m.TeamA).ThenInclude(a => a!.InvitedTeam)
            .Include(x => x.Matches).ThenInclude(m => m.TeamB).ThenInclude(b => b!.LvssTeam)
            .Include(x => x.Matches).ThenInclude(m => m.TeamB).ThenInclude(b => b!.InvitedTeam)
            .Include(x => x.Matches).ThenInclude(m => m.Field)
            .Include(x => x.Matches).ThenInclude(m => m.Day)
            .Include(x => x.Matches).ThenInclude(m => m.Tier)
            .FirstOrDefaultAsync(x => x.PublicSlug == s, ct);
        if (t is null) return NotFound();

        return Ok(new PublicScheduleDto(
            t.Name, t.Kind, t.StartDate, t.EndDate,
            t.Venue?.Name, t.Venue?.Address, t.Location,
            t.RulesOfPlay,
            t.Days.OrderBy(d => d.Date)
                .Select(d => new HostedTournamentDayDto(d.Id, d.Date, d.StartTime, d.EndTime, d.Notes, d.CreatedAt))
                .ToList(),
            t.Fields.OrderBy(f => f.SortOrder).ThenBy(f => f.Name)
                .Select(f => new HostedTournamentFieldDto(f.Id, f.VenueFieldId, f.Name, f.SortOrder, f.Notes, f.CreatedAt))
                .ToList(),
            t.Matches
                .OrderBy(m => m.Day?.Date).ThenBy(m => m.StartTime).ThenBy(m => m.Field?.SortOrder ?? 0)
                .Select(m => new HostedTournamentMatchDto(
                    m.Id,
                    m.TierId, m.Tier?.Name,
                    m.TeamAId, TeamLabel(m.TeamA),
                    m.TeamBId, TeamLabel(m.TeamB),
                    m.FieldId, m.Field?.Name,
                    m.DayId, m.Day?.Date,
                    m.StartTime, m.DurationMinutes,
                    // Strip admin notes from the public payload — those are internal.
                    null))
                .ToList()));
    }

    private static string? TeamLabel(HostedTournamentTeam? t) =>
        t?.LvssTeam?.Name ?? t?.InvitedTeam?.Name;
}
