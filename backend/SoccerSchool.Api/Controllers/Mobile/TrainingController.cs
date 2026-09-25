using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SoccerSchool.Api.Data;
using SoccerSchool.Api.Domain;
using SoccerSchool.Api.Dtos;
using SoccerSchool.Api.Services;

namespace SoccerSchool.Api.Controllers.Mobile;

/// <summary>
/// The Daily Training app's data: who am I, today's drills, mark a drill done, and progress
/// (streaks + last 7 days). Kid-only — PlayerJwt scheme.
/// </summary>
[ApiController]
[Route("api/mobile/training")]
[Authorize(AuthenticationSchemes = AuthSchemes.PlayerJwt)]
public class TrainingController : ControllerBase
{
    /// <summary>How far back streaks look. Longer streaks than this are reported as this.</summary>
    private const int StreakLookbackDays = 365;

    private readonly AppDbContext _db;
    private readonly ITrainingPlanService _plans;

    public TrainingController(AppDbContext db, ITrainingPlanService plans)
    {
        _db = db;
        _plans = plans;
    }

    [HttpGet("me")]
    public async Task<ActionResult<PlayerMeDto>> Me(CancellationToken ct)
    {
        var login = await CurrentLoginAsync(ct);
        if (login is null) return Unauthorized();
        return Ok(await BuildMeAsync(_db, login, ct));
    }

    [HttpGet("today")]
    public async Task<ActionResult<TodayPlanDto>> Today(CancellationToken ct)
    {
        var login = await CurrentLoginAsync(ct);
        if (login is null) return Unauthorized();

        var today = _plans.ClubToday();
        var drills = await DrillsForDayAsync(login.PlayerId, today, ct);
        var completions = await CompletionsOnAsync(login.PlayerId, today, ct);
        var activities = drills.Select(d => TrainingWire.ToActivity(d, completions.GetValueOrDefault(d.Id))).ToList();
        return Ok(new TodayPlanDto(today, activities));
    }

    [HttpGet("activities/{drillId:int}")]
    public async Task<ActionResult<TrainingActivityDto>> Activity(int drillId, CancellationToken ct)
    {
        var login = await CurrentLoginAsync(ct);
        if (login is null) return Unauthorized();

        var today = _plans.ClubToday();
        var drill = (await DrillsForDayAsync(login.PlayerId, today, ct)).FirstOrDefault(d => d.Id == drillId);
        if (drill is null) return NotFound();
        var completions = await CompletionsOnAsync(login.PlayerId, today, ct);
        return Ok(TrainingWire.ToActivity(drill, completions.GetValueOrDefault(drillId)));
    }

    /// <summary>Marks a drill done for today. Idempotent. Only drills on today's plan can be
    /// completed, so kids can't farm streaks from unassigned or expired drills.</summary>
    [HttpPost("activities/{drillId:int}/complete")]
    public async Task<ActionResult<TrainingActivityDto>> Complete(int drillId, CancellationToken ct)
    {
        var login = await CurrentLoginAsync(ct);
        if (login is null) return Unauthorized();

        var today = _plans.ClubToday();
        var drill = (await DrillsForDayAsync(login.PlayerId, today, ct)).FirstOrDefault(d => d.Id == drillId);
        if (drill is null) return NotFound();

        var completion = await _db.DrillCompletions.FirstOrDefaultAsync(c =>
            c.PlayerId == login.PlayerId && c.DrillId == drillId && c.Date == today, ct);
        if (completion is null)
        {
            completion = new DrillCompletion { PlayerId = login.PlayerId, DrillId = drillId, Date = today };
            _db.DrillCompletions.Add(completion);
            try
            {
                await _db.SaveChangesAsync(ct);
            }
            catch (DbUpdateException)
            {
                // Double-tap race: the other request already inserted today's row. Use that one.
                _db.Entry(completion).State = EntityState.Detached;
                completion = await _db.DrillCompletions.AsNoTracking().FirstAsync(c =>
                    c.PlayerId == login.PlayerId && c.DrillId == drillId && c.Date == today, ct);
            }
        }
        return Ok(TrainingWire.ToActivity(drill, completion));
    }

    [HttpGet("progress")]
    public async Task<ActionResult<TrainingProgressDto>> Progress(CancellationToken ct)
    {
        var login = await CurrentLoginAsync(ct);
        if (login is null) return Unauthorized();

        var today = _plans.ClubToday();
        var weekStart = today.AddDays(-6);
        var lookbackStart = today.AddDays(-StreakLookbackDays);

        var completedDays = await _db.DrillCompletions
            .Where(c => c.PlayerId == login.PlayerId && c.Date >= lookbackStart)
            .GroupBy(c => c.Date)
            .Select(g => new { Date = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Date, x => x.Count, ct);
        var totalCompleted = await _db.DrillCompletions.CountAsync(c => c.PlayerId == login.PlayerId, ct);

        // Current streak: consecutive days with at least one drill done, ending today — or
        // yesterday, so the streak doesn't show 0 all morning before today's drills are done.
        var current = 0;
        var cursor = completedDays.ContainsKey(today) ? today : today.AddDays(-1);
        while (cursor >= lookbackStart && completedDays.ContainsKey(cursor))
        {
            current++;
            cursor = cursor.AddDays(-1);
        }

        var best = 0;
        var run = 0;
        for (var d = lookbackStart; d <= today; d = d.AddDays(1))
        {
            run = completedDays.ContainsKey(d) ? run + 1 : 0;
            best = Math.Max(best, run);
        }

        var targets = await _plans.GetTargetsAsync(login.PlayerId, ct);
        var assignments = await _plans.GetAssignmentsAsync(targets, weekStart, today, ct);
        var last7 = new List<DayProgressDto>(7);
        for (var d = weekStart; d <= today; d = d.AddDays(1))
        {
            var total = _plans.DrillsOn(assignments, d).Count;
            var done = completedDays.GetValueOrDefault(d);
            // Completions of since-unassigned drills still count, so total never reads below done.
            last7.Add(new DayProgressDto(d, done, Math.Max(total, done)));
        }

        return Ok(new TrainingProgressDto(current, best, totalCompleted, last7));
    }

    // ---------------------------------------------------------------------------------------

    /// <summary>Resolves the signed-in kid from the token, re-checking the database each request so
    /// a parent removing the login — or changing the password — takes effect immediately.</summary>
    private async Task<PlayerLogin?> CurrentLoginAsync(CancellationToken ct)
    {
        var raw = User.FindFirst(PlayerTokenService.PlayerLoginIdClaim)?.Value;
        if (!int.TryParse(raw, out var loginId)) return null;

        var login = await _db.PlayerLogins.Include(l => l.Player).FirstOrDefaultAsync(l => l.Id == loginId, ct);
        if (login is null) return null;

        var version = User.FindFirst(PlayerTokenService.PasswordVersionClaim)?.Value;
        if (!long.TryParse(version, out var v) || v < PlayerTokenService.PasswordVersion(login))
            return null;

        return login;
    }

    private async Task<List<Drill>> DrillsForDayAsync(int playerId, DateOnly day, CancellationToken ct)
    {
        var targets = await _plans.GetTargetsAsync(playerId, ct);
        var assignments = await _plans.GetAssignmentsAsync(targets, day, day, ct);
        return _plans.DrillsOn(assignments, day);
    }

    private Task<Dictionary<int, DrillCompletion>> CompletionsOnAsync(int playerId, DateOnly day, CancellationToken ct) =>
        _db.DrillCompletions
            .AsNoTracking()
            .Where(c => c.PlayerId == playerId && c.Date == day)
            .ToDictionaryAsync(c => c.DrillId, ct);

    internal static async Task<PlayerMeDto> BuildMeAsync(AppDbContext db, PlayerLogin login, CancellationToken ct)
    {
        var player = login.Player ?? await db.Players.FirstAsync(p => p.Id == login.PlayerId, ct);
        // Most recently joined team, matching the admin players list's "current team".
        var teamName = await db.TeamPlayers
            .Where(tp => tp.PlayerId == player.Id)
            .OrderByDescending(tp => tp.AddedAt)
            .Select(tp => tp.Team!.Name)
            .FirstOrDefaultAsync(ct);
        return new PlayerMeDto(player.Id, player.FirstName, player.LastName, login.Username, teamName);
    }
}
