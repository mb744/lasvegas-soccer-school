using Microsoft.EntityFrameworkCore;
using SoccerSchool.Api.Data;

namespace SoccerSchool.Api.Services;

public enum TeamDeleteOutcome { Deleted, NotFound, Blocked }

public record TeamDeleteResult(TeamDeleteOutcome Outcome, string? Message = null);

/// <summary>
/// Deletes a team. Most dependents cascade in the database (roster, coaches, games, attendance,
/// drills) or are unlinked (chat groups, announcements), but two links block the delete:
/// <list type="bullet">
/// <item>External tournament entries (<c>TournamentTeams</c>, FK Restrict) are just "this team
/// entered X" links, so they're removed with the team.</item>
/// <item>Hosted tournament entries carry brackets, matches and scores, so deleting the team is
/// refused with a message naming the tournament(s) instead of silently breaking them.</item>
/// </list>
/// </summary>
public interface ITeamDeletionService
{
    Task<TeamDeleteResult> DeleteAsync(int teamId, CancellationToken ct);
}

public class TeamDeletionService : ITeamDeletionService
{
    private readonly AppDbContext _db;

    public TeamDeletionService(AppDbContext db) => _db = db;

    public async Task<TeamDeleteResult> DeleteAsync(int teamId, CancellationToken ct)
    {
        var team = await _db.Teams.FindAsync(new object?[] { teamId }, ct);
        if (team is null) return new TeamDeleteResult(TeamDeleteOutcome.NotFound);

        var hosted = await _db.HostedTournamentTeams
            .Where(h => h.LvssTeamId == teamId)
            .Select(h => h.HostedTournament!.Name)
            .Distinct()
            .OrderBy(n => n)
            .ToListAsync(ct);
        if (hosted.Count > 0)
        {
            var list = string.Join(", ", hosted.Select(n => $"\"{n}\""));
            return new TeamDeleteResult(TeamDeleteOutcome.Blocked, hosted.Count == 1
                ? $"{team.Name} is entered in the hosted tournament {list}. Remove it from that tournament first, then delete the team."
                : $"{team.Name} is entered in the hosted tournaments {list}. Remove it from those tournaments first, then delete the team.");
        }

        var entries = await _db.TournamentTeams.Where(t => t.TeamId == teamId).ToListAsync(ct);
        _db.TournamentTeams.RemoveRange(entries);
        _db.Teams.Remove(team);
        await _db.SaveChangesAsync(ct); // one transaction: entries and team go together
        return new TeamDeleteResult(TeamDeleteOutcome.Deleted);
    }
}
