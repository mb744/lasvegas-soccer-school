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
            .Include(x => x.Tiers).ThenInclude(tr => tr.Brackets)
            .Include(x => x.Teams).ThenInclude(tt => tt.LvssTeam)
            .Include(x => x.Teams).ThenInclude(tt => tt.InvitedTeam)
            .Include(x => x.Teams).ThenInclude(tt => tt.Bracket)
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
                    m.TeamAScore, m.TeamBScore,
                    // Strip admin notes from the public payload — those are internal.
                    null))
                .ToList(),
            BuildStandings(t)));
    }

    /// <summary>Group teams by bracket (falling back to the tier's own bucket for teams that
    /// were tier-assigned but not bracketed), compute per-team GP/W/D/L/GF/GA/GD/Pts across all
    /// played matches (both scores set), then sort each group by Points → GD → GF.</summary>
    private static List<BracketStandingsDto> BuildStandings(HostedTournament t)
    {
        // A "played" match is one where both scores are set. Unplayed matches (both scores null)
        // are skipped so the standings only reflect real results. If only one side has a score
        // that's treated as data-entry mid-edit and also skipped.
        var played = t.Matches
            .Where(m => m.TeamAId.HasValue && m.TeamBId.HasValue && m.TeamAScore.HasValue && m.TeamBScore.HasValue)
            .ToList();

        var out_ = new List<BracketStandingsDto>();

        // Bucket teams by bracket. Teams with no bracket AND no tier land in a shared "Unassigned"
        // group so they still show up on the standings block — hides in the frontend if empty.
        foreach (var tier in t.Tiers.OrderBy(x => x.SortOrder).ThenBy(x => x.Name))
        {
            foreach (var br in tier.Brackets.OrderBy(x => x.SortOrder).ThenBy(x => x.Name))
            {
                var teams = t.Teams.Where(tt => tt.BracketId == br.Id).ToList();
                if (teams.Count == 0) continue;
                out_.Add(new BracketStandingsDto(br.Id, br.Name, tier.Name, ComputeRows(teams, played)));
            }
            // Teams inside this tier but not bracketed yet.
            var unBracketed = t.Teams.Where(tt => tt.TierId == tier.Id && tt.BracketId == null).ToList();
            if (unBracketed.Count > 0)
                out_.Add(new BracketStandingsDto(null, $"{tier.Name} — unbracketed", tier.Name, ComputeRows(unBracketed, played)));
        }
        var noTier = t.Teams.Where(tt => tt.TierId == null && tt.BracketId == null).ToList();
        if (noTier.Count > 0)
            out_.Add(new BracketStandingsDto(null, "Unassigned", null, ComputeRows(noTier, played)));
        return out_;
    }

    private static IReadOnlyList<BracketStandingRowDto> ComputeRows(
        IReadOnlyList<HostedTournamentTeam> teams,
        IReadOnlyList<HostedTournamentMatch> played)
    {
        var teamIds = teams.Select(t => t.Id).ToHashSet();
        return teams
            .Select(team =>
            {
                int g = 0, w = 0, d = 0, l = 0, gf = 0, ga = 0;
                foreach (var m in played)
                {
                    if (m.TeamAId == team.Id && teamIds.Contains(m.TeamBId!.Value))
                    {
                        g++; gf += m.TeamAScore!.Value; ga += m.TeamBScore!.Value;
                        if (m.TeamAScore > m.TeamBScore) w++;
                        else if (m.TeamAScore < m.TeamBScore) l++;
                        else d++;
                    }
                    else if (m.TeamBId == team.Id && teamIds.Contains(m.TeamAId!.Value))
                    {
                        g++; gf += m.TeamBScore!.Value; ga += m.TeamAScore!.Value;
                        if (m.TeamBScore > m.TeamAScore) w++;
                        else if (m.TeamBScore < m.TeamAScore) l++;
                        else d++;
                    }
                }
                return new BracketStandingRowDto(
                    TeamId: team.Id,
                    TeamName: TeamLabel(team) ?? "—",
                    GamesPlayed: g,
                    Wins: w,
                    Draws: d,
                    Losses: l,
                    GoalsFor: gf,
                    GoalsAgainst: ga,
                    GoalDifferential: gf - ga,
                    Points: 3 * w + d);
            })
            .OrderByDescending(r => r.Points)
            .ThenByDescending(r => r.GoalDifferential)
            .ThenByDescending(r => r.GoalsFor)
            .ThenBy(r => r.TeamName)
            .ToList();
    }

    private static string? TeamLabel(HostedTournamentTeam? t) =>
        t?.LvssTeam?.Name ?? t?.InvitedTeam?.Name;
}
