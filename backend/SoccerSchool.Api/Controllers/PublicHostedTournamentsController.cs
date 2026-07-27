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

        var knockout = BuildKnockout(t);
        // Build a (tierId, slot) → (labelA, labelB) lookup so playoff matches whose teams
        // haven't been assigned yet can display the projected seed placeholders — e.g.
        // "West 1st Place vs East 2nd Place" — instead of empty strings on the public page.
        var playoffLabels = new Dictionary<(int? TierId, PlayoffSlot Slot), (string A, string B)>();
        foreach (var stage in knockout)
        {
            var tier = t.Tiers.FirstOrDefault(x => x.Name == stage.TierName);
            var tierId = tier?.Id;
            for (var i = 0; i < stage.Matches.Count; i++)
            {
                var slot = (PlayoffSlot)(i + 1); // matches enum order SF1, SF2, Consolation, Final
                playoffLabels[(tierId, slot)] = (stage.Matches[i].TeamALabel, stage.Matches[i].TeamBLabel);
            }
        }

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
                // Same sort as the admin projection so the public schedule ends with
                // SF1 → SF2 → Consolation → Final under any group-stage rows.
                .OrderBy(m => m.PlayoffSlot.HasValue ? 1 : 0)
                .ThenBy(m => m.PlayoffSlot)
                .ThenBy(m => m.Day?.Date).ThenBy(m => m.StartTime).ThenBy(m => m.Field?.SortOrder ?? 0)
                .Select(m => {
                    var labelA = TeamLabel(m.TeamA);
                    var labelB = TeamLabel(m.TeamB);
                    // For playoff placeholders with null teams, fall through to the projected
                    // seed labels from the knockout stage so the schedule row shows something
                    // meaningful ("West 1st Place vs East 2nd Place") instead of "TBD vs TBD".
                    if (m.PlayoffSlot is PlayoffSlot ps
                        && playoffLabels.TryGetValue((m.TierId, ps), out var proj))
                    {
                        labelA ??= proj.A;
                        labelB ??= proj.B;
                    }
                    return new HostedTournamentMatchDto(
                        m.Id,
                        m.TierId, m.Tier?.Name,
                        m.TeamAId, labelA,
                        m.TeamBId, labelB,
                        m.FieldId, m.Field?.Name,
                        m.DayId, m.Day?.Date,
                        m.StartTime, m.DurationMinutes,
                        m.TeamAScore, m.TeamBScore,
                        // Strip admin notes from the public payload — those are internal.
                        null,
                        m.PlayoffSlot);
                })
                .ToList(),
            BuildStandings(t),
            knockout));
    }

    /// <summary>For every tier that has exactly two brackets, project a Semifinal 1 (A#1 vs B#2),
    /// Semifinal 2 (B#1 vs A#2), Consolation (A#3 vs B#3), and Final (SF1 winner vs SF2 winner).
    /// Team names come from the standings when seeds are settled; slots fall back to seed labels
    /// like "West #1" when the standings haven't converged yet. When a scheduled match exists
    /// between the resolved teams and both scores are set, the slot pulls those scores + the
    /// winner.</summary>
    private static List<KnockoutStageDto> BuildKnockout(HostedTournament t)
    {
        var played = t.Matches
            .Where(m => m.TeamAId.HasValue && m.TeamBId.HasValue && m.TeamAScore.HasValue && m.TeamBScore.HasValue)
            .ToList();
        var stages = new List<KnockoutStageDto>();
        foreach (var tier in t.Tiers.OrderBy(x => x.SortOrder).ThenBy(x => x.Name))
        {
            var brackets = tier.Brackets.OrderBy(b => b.SortOrder).ThenBy(b => b.Name).ToList();
            if (brackets.Count != 2) continue; // knockout template assumes two brackets
            var a = brackets[0];
            var b = brackets[1];
            var aTeams = t.Teams.Where(tt => tt.BracketId == a.Id).ToList();
            var bTeams = t.Teams.Where(tt => tt.BracketId == b.Id).ToList();
            var aStandings = ComputeRows(aTeams, played);
            var bStandings = ComputeRows(bTeams, played);

            // A bracket is "settled" only once every team in it has finished the round-robin
            // schedule (every team's GamesPlayed hits the expected count). Until then we show
            // seed placeholders like "West 1st Place" instead of real team names, so the
            // playoff card doesn't imply a team is locked in before the group stage is done.
            // Expected games per team depends on whether the tier crosses brackets:
            //   * CrossBracketPlay=true → each team plays every team in the OTHER bracket
            //   * CrossBracketPlay=false → each team plays every other team in ITS bracket
            var aExpected = tier.CrossBracketPlay ? bTeams.Count : Math.Max(0, aTeams.Count - 1);
            var bExpected = tier.CrossBracketPlay ? aTeams.Count : Math.Max(0, bTeams.Count - 1);
            var aSettled = aTeams.Count > 0 && aStandings.All(r => r.GamesPlayed >= aExpected);
            var bSettled = bTeams.Count > 0 && bStandings.All(r => r.GamesPlayed >= bExpected);

            static string Ordinal(int n) => n switch { 1 => "1st", 2 => "2nd", 3 => "3rd", _ => n + "th" };
            // Placeholder label when the bracket isn't fully played yet.
            static string Placeholder(HostedTournamentBracket br, int seed) => $"{br.Name} {Ordinal(seed)} Place";

            // Label the slot — real team name only once the source bracket has played every
            // scheduled match; placeholder otherwise.
            string SeedLabel(HostedTournamentBracket br, IReadOnlyList<BracketStandingRowDto> rows, int seed, bool settled) =>
                (settled && rows.Count >= seed) ? rows[seed - 1].TeamName : Placeholder(br, seed);
            int? SeedTeamId(IReadOnlyList<BracketStandingRowDto> rows, int seed, bool settled) =>
                (settled && rows.Count >= seed) ? rows[seed - 1].TeamId : null;

            KnockoutMatchDto MakeSlot(string slotName, string labelA, string labelB, int? teamAId, int? teamBId)
            {
                int? scoreA = null, scoreB = null;
                string? winner = null;
                if (teamAId.HasValue && teamBId.HasValue)
                {
                    // Prefer the most recent played match between the two teams so the display
                    // always reflects the latest result if an admin corrected a score.
                    var m = played
                        .Where(x =>
                            (x.TeamAId == teamAId && x.TeamBId == teamBId) ||
                            (x.TeamAId == teamBId && x.TeamBId == teamAId))
                        .OrderByDescending(x => x.Day?.Date).ThenByDescending(x => x.StartTime)
                        .FirstOrDefault();
                    if (m != null)
                    {
                        var aIsHome = m.TeamAId == teamAId;
                        scoreA = aIsHome ? m.TeamAScore : m.TeamBScore;
                        scoreB = aIsHome ? m.TeamBScore : m.TeamAScore;
                        if (scoreA > scoreB) winner = labelA;
                        else if (scoreB > scoreA) winner = labelB;
                        // Draws in knockout have no natural winner; leave null so the frontend
                        // can render "Tied" and the admin knows to break the tie manually.
                    }
                }
                return new KnockoutMatchDto(slotName, labelA, labelB, scoreA, scoreB, winner);
            }

            var sf1 = MakeSlot("Semifinal 1",
                SeedLabel(a, aStandings, 1, aSettled), SeedLabel(b, bStandings, 2, bSettled),
                SeedTeamId(aStandings, 1, aSettled), SeedTeamId(bStandings, 2, bSettled));
            var sf2 = MakeSlot("Semifinal 2",
                SeedLabel(b, bStandings, 1, bSettled), SeedLabel(a, aStandings, 2, aSettled),
                SeedTeamId(bStandings, 1, bSettled), SeedTeamId(aStandings, 2, aSettled));
            var consolation = MakeSlot("Consolation (3rd place)",
                SeedLabel(a, aStandings, 3, aSettled), SeedLabel(b, bStandings, 3, bSettled),
                SeedTeamId(aStandings, 3, aSettled), SeedTeamId(bStandings, 3, bSettled));

            // Final teams derive from the semifinal winners — only resolvable once both SFs
            // have real scores. Otherwise show "Winner SF1 / SF2" placeholders.
            string finalA = sf1.WinnerLabel ?? "Winner Semifinal 1";
            string finalB = sf2.WinnerLabel ?? "Winner Semifinal 2";
            // Look up the actual team ids behind those winner labels so a scheduled Final can
            // still get scores wired in from a real played match.
            int? finalAId = sf1.WinnerLabel != null
                ? (sf1.WinnerLabel == sf1.TeamALabel ? SeedTeamId(aStandings, 1, aSettled) : SeedTeamId(bStandings, 2, bSettled))
                : null;
            int? finalBId = sf2.WinnerLabel != null
                ? (sf2.WinnerLabel == sf2.TeamALabel ? SeedTeamId(bStandings, 1, bSettled) : SeedTeamId(aStandings, 2, aSettled))
                : null;
            var final = MakeSlot("Final", finalA, finalB, finalAId, finalBId);

            stages.Add(new KnockoutStageDto(tier.Name, a.Name, b.Name,
                new[] { sf1, sf2, consolation, final }.ToList()));
        }
        return stages;
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
