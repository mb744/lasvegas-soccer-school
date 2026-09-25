using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SoccerSchool.Api.Data;
using SoccerSchool.Api.Domain;
using SoccerSchool.Api.Options;

namespace SoccerSchool.Api.Services;

/// <summary>Everything a drill assignment can target, resolved for one player.</summary>
public record PlayerTargets(int PlayerId, IReadOnlyList<int> TeamIds, int? AgeClassificationId);

/// <summary>
/// Works out which drills a kid should do on a given day. A kid's plan is the de-duplicated union
/// of every active assignment that reaches them directly, through any team they're rostered on, or
/// through their age classification.
/// </summary>
public interface ITrainingPlanService
{
    /// <summary>Today's date in the club's time zone (Las Vegas). "Today" for a kid must not flip
    /// at 4pm local because the server runs on UTC.</summary>
    DateOnly ClubToday();

    Task<PlayerTargets> GetTargetsAsync(int playerId, CancellationToken ct);

    /// <summary>Assignments (with their active drills loaded) reaching the player whose date range
    /// overlaps [from, to].</summary>
    Task<List<DrillAssignment>> GetAssignmentsAsync(PlayerTargets targets, DateOnly from, DateOnly to, CancellationToken ct);

    /// <summary>The distinct drills the given assignments put on the plan for <paramref name="date"/>,
    /// in plan order (warm-up first, cool-down last).</summary>
    List<Drill> DrillsOn(IEnumerable<DrillAssignment> assignments, DateOnly date);
}

public class TrainingPlanService : ITrainingPlanService
{
    private static readonly TimeZoneInfo ClubTimeZone = TimeZoneInfo.FindSystemTimeZoneById("America/Los_Angeles");

    private readonly AppDbContext _db;
    private readonly AppOptions _app;

    public TrainingPlanService(AppDbContext db, IOptions<AppOptions> app)
    {
        _db = db;
        _app = app.Value;
    }

    public DateOnly ClubToday() =>
        DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, ClubTimeZone));

    public async Task<PlayerTargets> GetTargetsAsync(int playerId, CancellationToken ct)
    {
        var teamIds = await _db.TeamPlayers
            .Where(tp => tp.PlayerId == playerId)
            .Select(tp => tp.TeamId)
            .ToListAsync(ct);

        // Same precedence the admin screens use: the bracket stamped on the active-season
        // registration, else whichever classification's DOB window contains the birthday.
        var ageClassificationId = await _db.RegistrationPlayers
            .Where(rp => rp.PlayerId == playerId
                && rp.Registration!.Season == _app.ActiveSeason
                && rp.AgeClassificationId != null)
            .OrderByDescending(rp => rp.Id)
            .Select(rp => rp.AgeClassificationId)
            .FirstOrDefaultAsync(ct);

        if (ageClassificationId is null)
        {
            var dob = await _db.Players.Where(p => p.Id == playerId).Select(p => p.DateOfBirth).FirstAsync(ct);
            ageClassificationId = await _db.AgeClassifications
                .Where(c => c.DobStart <= dob && dob <= c.DobEnd)
                .OrderBy(c => c.Id)
                .Select(c => (int?)c.Id)
                .FirstOrDefaultAsync(ct);
        }

        return new PlayerTargets(playerId, teamIds, ageClassificationId);
    }

    public Task<List<DrillAssignment>> GetAssignmentsAsync(PlayerTargets targets, DateOnly from, DateOnly to, CancellationToken ct)
    {
        var teamIds = targets.TeamIds.ToList();
        var ageId = targets.AgeClassificationId;
        return _db.DrillAssignments
            .AsNoTracking()
            .Include(a => a.Drill)
            .Where(a => a.Drill!.IsActive
                && a.StartDate <= to
                && (a.EndDate == null || a.EndDate >= from)
                && ((a.TargetType == DrillTargetType.Player && a.PlayerId == targets.PlayerId)
                    || (a.TargetType == DrillTargetType.Team && a.TeamId != null && teamIds.Contains(a.TeamId.Value))
                    || (a.TargetType == DrillTargetType.AgeClassification && ageId != null && a.AgeClassificationId == ageId)))
            .ToListAsync(ct);
    }

    public List<Drill> DrillsOn(IEnumerable<DrillAssignment> assignments, DateOnly date) =>
        assignments
            .Where(a => a.StartDate <= date && (a.EndDate == null || a.EndDate >= date))
            .Select(a => a.Drill!)
            .DistinctBy(d => d.Id)
            .OrderBy(d => d.Category)
            .ThenBy(d => d.Id)
            .ToList();
}
