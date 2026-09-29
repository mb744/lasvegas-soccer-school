using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using SoccerSchool.Api.Controllers.Mobile;
using SoccerSchool.Api.Domain;
using SoccerSchool.Api.Dtos;
using SoccerSchool.Api.Services;

namespace SoccerSchool.Api.Tests;

public class MobileRosterTests
{
    private static MobileRosterController As(Harness h, ApplicationUser user) => new(h.Db, h.Services.GetRequiredService<ICoachScopeService>())
    {
        ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { User = Harness.Principal(user) } },
    };

    private static T Ok<T>(ActionResult<T> r) => Assert.IsAssignableFrom<T>(Assert.IsType<OkObjectResult>(r.Result).Value);

    /// <summary>Family with a primary parent, a co-parent contact, a duplicate contact and a
    /// view-only grandparent; one kid on the team with an active and a returned jersey.</summary>
    private static async Task<int> KidOnTeamAsync(Harness h, int teamId, string first, string last, string jersey)
    {
        var parentLogin = await h.UserAsync($"{first.ToLowerInvariant()}.parent@test");
        var family = new ParentAccount { UserId = parentLogin.Id, FirstName = "Maria", LastName = last, CellPhone = "+17025550100", Language = Language.Spanish };
        h.Db.ParentAccounts.Add(family);
        await h.Db.SaveChangesAsync();
        var kid = new Player { ParentAccountId = family.Id, FirstName = first, LastName = last, DateOfBirth = new DateOnly(2016, 1, 1) };
        var uniform = new Uniform { Name = "Home" };
        h.Db.AddRange(kid, uniform);
        await h.Db.SaveChangesAsync();
        h.Db.TeamPlayers.Add(new TeamPlayer { TeamId = teamId, PlayerId = kid.Id });
        h.Db.PlayerUniformAssignments.AddRange(
            new PlayerUniformAssignment { PlayerId = kid.Id, UniformId = uniform.Id, JerseyNumber = jersey, AssignedAt = new DateOnly(2026, 8, 1) },
            new PlayerUniformAssignment { PlayerId = kid.Id, UniformId = uniform.Id, JerseyNumber = "99", AssignedAt = new DateOnly(2025, 8, 1), ReturnedAt = new DateOnly(2026, 1, 1) });
        h.Db.ParentContacts.AddRange(
            new ParentContact { ParentAccountId = family.Id, FirstName = "Jose", LastName = last, CellPhone = "+17025550101", Email = "jose@test", Language = Language.English },
            new ParentContact { ParentAccountId = family.Id, FirstName = "Maria", LastName = "Dup", CellPhone = "+17025550100" },
            new ParentContact { ParentAccountId = family.Id, FirstName = "Abuela", LastName = last, Email = "abuela@test", AccessLevel = FamilyAccessLevel.Viewer });
        await h.Db.SaveChangesAsync();
        return kid.Id;
    }

    [Fact]
    public async Task Coach_sees_only_their_team_with_jerseys_and_guardians()
    {
        await using var h = new Harness();
        var coach = await h.UserAsync("coach@test");
        var myTeam = await h.CoachAsync(coach);
        var otherTeam = await h.CoachAsync(await h.UserAsync("other@test"));
        var kid = await KidOnTeamAsync(h, myTeam, "Ana", "Lopez", "10");
        var otherKid = await KidOnTeamAsync(h, otherTeam, "Leo", "Ruiz", "7");

        var api = As(h, coach);

        var teams = Ok(await api.Teams(default));
        Assert.Equal(new[] { myTeam }, teams.Select(t => t.Id));

        var roster = Ok(await api.Roster(myTeam, default));
        var row = Assert.Single(roster.Players);
        Assert.Equal(("Ana", "Lopez"), (row.FirstName, row.LastName));
        Assert.Equal(new[] { "10" }, row.JerseyNumbers); // returned jersey #99 excluded

        var detail = Ok(await api.Player(myTeam, kid, default));
        Assert.Collection(detail.Guardians,
            g => { Assert.True(g.IsPrimary); Assert.Equal("Maria Lopez", g.Name); Assert.Equal("ana.parent@test", g.Email); Assert.Equal(Language.Spanish, g.Language); },
            g => { Assert.False(g.IsPrimary); Assert.Equal("Jose Lopez", g.Name); Assert.Equal("+17025550101", g.Phone); });
        // Duplicate of the primary and the view-only grandparent aren't listed.

        Assert.IsType<ForbidResult>((await api.Roster(otherTeam, default)).Result);
        Assert.IsType<ForbidResult>((await api.Player(otherTeam, otherKid, default)).Result);
        // A kid from another team can't be read through the coach's own team id.
        Assert.IsType<NotFoundResult>((await api.Player(myTeam, otherKid, default)).Result);
    }

    [Fact]
    public async Task Admin_sees_every_team()
    {
        await using var h = new Harness();
        var t1 = await h.CoachAsync(await h.UserAsync("c1@test"));
        var t2 = await h.CoachAsync(await h.UserAsync("c2@test"));
        var admin = await h.UserAsync("admin@test", admin: true);

        var teams = Ok(await As(h, admin).Teams(default));
        Assert.Equal(new[] { t1, t2 }.OrderBy(x => x), teams.Select(t => t.Id).OrderBy(x => x));
    }
}
