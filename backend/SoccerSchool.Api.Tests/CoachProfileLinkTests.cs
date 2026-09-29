using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using SoccerSchool.Api.Controllers;
using SoccerSchool.Api.Domain;
using SoccerSchool.Api.Dtos;
using SoccerSchool.Api.Options;
using SoccerSchool.Api.Services;

namespace SoccerSchool.Api.Tests;

public class CoachProfileLinkTests
{
    private static ICoachScopeService Scope(Harness h) => h.Services.GetRequiredService<ICoachScopeService>();

    /// <summary>A coach profile plus a team whose card points at it, with a card email that
    /// deliberately differs from any login.</summary>
    private static async Task<(Coach Profile, int TeamId)> ProfileOnTeamAsync(Harness h, string profileEmail)
    {
        var profile = new Coach { FirstName = "Carlos", LastName = "Diaz", Email = profileEmail };
        var team = new Team { Name = "U11 Red" };
        h.Db.AddRange(profile, team);
        await h.Db.SaveChangesAsync();
        h.Db.TeamCoaches.Add(new TeamCoach { TeamId = team.Id, CoachId = profile.Id, Name = "Carlos Diaz", Email = "old-address@test" });
        await h.Db.SaveChangesAsync();
        return (profile, team.Id);
    }

    [Fact]
    public async Task Admin_linked_login_coaches_the_profiles_teams_even_with_a_different_email()
    {
        await using var h = new Harness();
        var (profile, teamId) = await ProfileOnTeamAsync(h, "carlos@test");
        var login = await h.UserAsync("carlos.personal@test", confirmed: false); // different + unverified

        var controller = new CoachesController(h.Db, null!, Microsoft.Extensions.Options.Options.Create(new AppOptions()));
        var result = await controller.SetLogin(profile.Id, new SetCoachLoginRequest { UserId = login.Id }, default);
        var dto = Assert.IsType<CoachDto>(Assert.IsType<OkObjectResult>(result.Result).Value);
        Assert.Equal("carlos.personal@test", dto.LinkedUserEmail);

        Assert.Equal(new[] { teamId }, await Scope(h).GetCoachTeamIdsAsync(login, default));
    }

    [Fact]
    public async Task Verified_matching_email_links_the_profile_automatically()
    {
        await using var h = new Harness();
        var (profile, teamId) = await ProfileOnTeamAsync(h, "Carlos@Test");
        var login = await h.UserAsync("carlos@test", confirmed: true);

        Assert.Equal(new[] { teamId }, await Scope(h).GetCoachTeamIdsAsync(login, default));
        Assert.Equal(login.Id, (await h.Db.Coaches.AsNoTracking().SingleAsync(c => c.Id == profile.Id)).UserId);
    }

    [Fact]
    public async Task Unverified_matching_email_does_not_link()
    {
        await using var h = new Harness();
        var (profile, _) = await ProfileOnTeamAsync(h, "carlos@test");
        var login = await h.UserAsync("carlos@test", confirmed: false);

        Assert.Empty(await Scope(h).GetCoachTeamIdsAsync(login, default));
        Assert.Null((await h.Db.Coaches.AsNoTracking().SingleAsync(c => c.Id == profile.Id)).UserId);
    }

    [Fact]
    public async Task A_login_can_only_belong_to_one_profile()
    {
        await using var h = new Harness();
        var (first, _) = await ProfileOnTeamAsync(h, "a@test");
        var second = new Coach { FirstName = "Other", LastName = "Coach" };
        h.Db.Coaches.Add(second);
        await h.Db.SaveChangesAsync();
        var login = await h.UserAsync("login@test");

        var controller = new CoachesController(h.Db, null!, Microsoft.Extensions.Options.Options.Create(new AppOptions()));
        Assert.IsType<OkObjectResult>((await controller.SetLogin(first.Id, new SetCoachLoginRequest { UserId = login.Id }, default)).Result);
        var conflict = Assert.IsType<ConflictObjectResult>((await controller.SetLogin(second.Id, new SetCoachLoginRequest { UserId = login.Id }, default)).Result);
        Assert.Contains("Carlos Diaz", conflict.Value as string);

        // Unlinking frees it.
        Assert.IsType<OkObjectResult>((await controller.SetLogin(first.Id, new SetCoachLoginRequest { UserId = null }, default)).Result);
        Assert.IsType<OkObjectResult>((await controller.SetLogin(second.Id, new SetCoachLoginRequest { UserId = login.Id }, default)).Result);
    }
}
