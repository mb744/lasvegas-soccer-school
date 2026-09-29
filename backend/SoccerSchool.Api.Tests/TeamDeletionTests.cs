using Microsoft.EntityFrameworkCore;
using SoccerSchool.Api.Domain;
using SoccerSchool.Api.Services;

namespace SoccerSchool.Api.Tests;

// The in-memory provider doesn't enforce foreign keys, so these check the service's decisions;
// the SQL Server constraint behavior itself was reproduced against LocalDB when this was written.
public class TeamDeletionTests
{
    private static async Task<int> TeamAsync(Harness h, string name = "U10")
    {
        var t = new Team { Name = name };
        h.Db.Teams.Add(t);
        await h.Db.SaveChangesAsync();
        return t.Id;
    }

    [Fact]
    public async Task External_tournament_entries_are_removed_with_the_team()
    {
        await using var h = new Harness();
        var teamId = await TeamAsync(h);
        var tour = new Tournament { Name = "Vegas Cup" };
        h.Db.Tournaments.Add(tour);
        await h.Db.SaveChangesAsync();
        h.Db.TournamentTeams.Add(new TournamentTeam { TournamentId = tour.Id, TeamId = teamId });
        await h.Db.SaveChangesAsync();

        var result = await new TeamDeletionService(h.Db).DeleteAsync(teamId, default);

        Assert.Equal(TeamDeleteOutcome.Deleted, result.Outcome);
        Assert.False(await h.Db.Teams.AnyAsync(t => t.Id == teamId));
        Assert.False(await h.Db.TournamentTeams.AnyAsync(t => t.TeamId == teamId));
        Assert.True(await h.Db.Tournaments.AnyAsync(t => t.Id == tour.Id)); // the tournament itself stays
    }

    [Fact]
    public async Task Hosted_tournament_entry_blocks_the_delete_and_names_the_tournament()
    {
        await using var h = new Harness();
        var teamId = await TeamAsync(h, "U12 Blue");
        var ht = new HostedTournament { Name = "LVSS Classic", StartDate = new DateOnly(2026, 10, 3) };
        h.Db.HostedTournaments.Add(ht);
        await h.Db.SaveChangesAsync();
        h.Db.HostedTournamentTeams.Add(new HostedTournamentTeam { HostedTournamentId = ht.Id, LvssTeamId = teamId });
        await h.Db.SaveChangesAsync();

        var result = await new TeamDeletionService(h.Db).DeleteAsync(teamId, default);

        Assert.Equal(TeamDeleteOutcome.Blocked, result.Outcome);
        Assert.Contains("\"LVSS Classic\"", result.Message);
        Assert.Contains("U12 Blue", result.Message);
        Assert.True(await h.Db.Teams.AnyAsync(t => t.Id == teamId));
    }

    [Fact]
    public async Task Missing_team_is_not_found()
    {
        await using var h = new Harness();
        Assert.Equal(TeamDeleteOutcome.NotFound, (await new TeamDeletionService(h.Db).DeleteAsync(999, default)).Outcome);
    }
}
