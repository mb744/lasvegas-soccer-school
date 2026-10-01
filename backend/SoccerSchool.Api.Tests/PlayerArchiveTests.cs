using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using SoccerSchool.Api.Controllers;
using SoccerSchool.Api.Domain;
using SoccerSchool.Api.Dtos;
using SoccerSchool.Api.Options;
using SoccerSchool.Api.Services;

namespace SoccerSchool.Api.Tests;

public class PlayerArchiveTests
{
    private sealed class FakeHasher : IReclaimHasher
    {
        public string? Hash(string? email) => string.IsNullOrWhiteSpace(email) ? null : "h:" + email.Trim().ToLowerInvariant();
    }

    private sealed class NoEmail : IEmailSender
    {
        public bool IsAvailable => false;
        public Task<EmailSendResult> SendAsync(string toEmail, string subject, string body, CancellationToken ct) =>
            Task.FromResult(new EmailSendResult(false, null, "off"));
        public Task<EmailSendResult> SendAsync(string toEmail, string subject, string plainText, string html, CancellationToken ct) =>
            Task.FromResult(new EmailSendResult(false, null, "off"));
    }

    private static readonly Microsoft.Extensions.Options.IOptions<AppOptions> App =
        Microsoft.Extensions.Options.Options.Create(new AppOptions());

    private static AdminPlayersController Players(Harness h) => new(h.Db, h.Users, new NoEmail(), App)
    {
        ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() },
    };

    private static async Task<(Team Team, Player Ana, Player Leo, ApplicationUser Mom)> SeedAsync(Harness h)
    {
        var mom = await h.UserAsync("mom@test");
        var other = await h.UserAsync("other@test");
        var lopez = new ParentAccount { UserId = mom.Id, FirstName = "Maria", LastName = "Lopez" };
        var smith = new ParentAccount { UserId = other.Id, FirstName = "Sam", LastName = "Smith" };
        var team = new Team { Name = "U11 Red" };
        h.Db.AddRange(lopez, smith, team);
        await h.Db.SaveChangesAsync();
        var ana = new Player { ParentAccountId = lopez.Id, FirstName = "Ana", LastName = "Lopez", DateOfBirth = new DateOnly(2016, 1, 1) };
        var leo = new Player { ParentAccountId = smith.Id, FirstName = "Leo", LastName = "Smith", DateOfBirth = new DateOnly(2016, 2, 2) };
        h.Db.Players.AddRange(ana, leo);
        await h.Db.SaveChangesAsync();
        h.Db.TeamPlayers.AddRange(new TeamPlayer { TeamId = team.Id, PlayerId = ana.Id }, new TeamPlayer { TeamId = team.Id, PlayerId = leo.Id });
        await h.Db.SaveChangesAsync();
        return (team, ana, leo, mom);
    }

    private static async Task<List<AdminPlayerSummaryDto>> ListAsync(Harness h, bool archived) =>
        Assert.IsAssignableFrom<IEnumerable<AdminPlayerSummaryDto>>(
            Assert.IsType<OkObjectResult>((await Players(h).List(null, default, archived)).Result).Value).ToList();

    [Fact]
    public async Task Archived_players_disappear_everywhere_and_come_back_on_their_teams()
    {
        await using var h = new Harness();
        var (team, ana, leo, _) = await SeedAsync(h);
        var teams = new TeamsController(h.Db, App);

        Assert.Equal(1, Assert.IsType<PlayerArchiveResult>(Assert.IsType<OkObjectResult>(
            (await Players(h).Archive(new PlayerIdsRequest { PlayerIds = new() { ana.Id } }, default)).Result).Value).Count);
        h.Db.ChangeTracker.Clear();

        // Gone from the players list, the roster, and the "add to team" picker.
        Assert.Equal(new[] { "Leo" }, (await ListAsync(h, archived: false)).Select(p => p.FirstName));
        Assert.Equal(new[] { leo.Id }, await h.Db.TeamPlayers.Where(tp => tp.TeamId == team.Id).Select(tp => tp.PlayerId).ToListAsync());
        var otherTeam = new Team { Name = "U12 Blue" };
        h.Db.Teams.Add(otherTeam);
        await h.Db.SaveChangesAsync();
        var available = Assert.IsAssignableFrom<IEnumerable<AvailablePlayerDto>>(
            Assert.IsType<OkObjectResult>((await teams.AvailablePlayers(otherTeam.Id, null, default)).Result).Value);
        Assert.DoesNotContain(available, p => p.PlayerId == ana.Id);
        // Adding by id is refused too.
        await teams.AddMembers(otherTeam.Id, new AddRosterMembersRequest { PlayerIds = new[] { ana.Id } }, default);
        Assert.False(await h.Db.TeamPlayers.IgnoreQueryFilters().AnyAsync(tp => tp.TeamId == otherTeam.Id && tp.PlayerId == ana.Id));

        // The archived list shows her, with her team, ready to unarchive.
        var archivedRow = Assert.Single(await ListAsync(h, archived: true));
        Assert.Equal(("Ana", "U11 Red", PlayerArchiveReason.Admin), (archivedRow.FirstName, archivedRow.CurrentTeamName, archivedRow.ArchivedReason!.Value));

        await Players(h).Unarchive(new PlayerIdsRequest { PlayerIds = new() { ana.Id } }, default);
        h.Db.ChangeTracker.Clear();
        Assert.Empty(await ListAsync(h, archived: true));
        Assert.Equal(2, await h.Db.TeamPlayers.CountAsync(tp => tp.TeamId == team.Id)); // back on her team
    }

    [Fact]
    public async Task A_deleted_family_kids_are_archived_and_return_if_the_family_signs_up_again()
    {
        await using var h = new Harness();
        var (team, ana, leo, mom) = await SeedAsync(h);

        var deletion = new AccountDeletionService(h.Db, h.Users, new FakeHasher(), NullLogger<AccountDeletionService>.Instance);
        await deletion.PurgeAsync(mom.Id, default);
        h.Db.ChangeTracker.Clear();

        var teams = new TeamsController(h.Db, App);
        var otherTeam = new Team { Name = "U12 Blue" };
        h.Db.Teams.Add(otherTeam);
        await h.Db.SaveChangesAsync();
        var available = Assert.IsAssignableFrom<IEnumerable<AvailablePlayerDto>>(
            Assert.IsType<OkObjectResult>((await teams.AvailablePlayers(otherTeam.Id, null, default)).Result).Value);
        Assert.Equal(new[] { leo.Id }, available.Select(p => p.PlayerId));

        var row = Assert.Single(await ListAsync(h, archived: true));
        Assert.Equal((ana.Id, PlayerArchiveReason.FamilyDeleted), (row.Id, row.ArchivedReason!.Value));

        // The family signs up again with the same email: the kid comes back automatically.
        var family = await h.Db.ParentAccounts.SingleAsync(a => a.ReclaimEmailHash != null);
        await PlayerArchive.RestoreFamilyAsync(h.Db, family.Id, default);
        await h.Db.SaveChangesAsync();
        h.Db.ChangeTracker.Clear();
        Assert.Equal(2, await h.Db.TeamPlayers.CountAsync(tp => tp.TeamId == team.Id));
    }
}
