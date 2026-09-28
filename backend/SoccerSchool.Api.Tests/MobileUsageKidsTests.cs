using Microsoft.AspNetCore.Mvc;
using SoccerSchool.Api.Controllers;
using SoccerSchool.Api.Domain;
using SoccerSchool.Api.Dtos;

namespace SoccerSchool.Api.Tests;

public class MobileUsageKidsTests
{
    [Fact]
    public async Task Linked_family_members_see_the_family_kids_count()
    {
        await using var h = new Harness();
        var mom = await h.UserAsync("mom@test");
        var dad = await h.UserAsync("dad@test");
        var grandma = await h.UserAsync("grandma@test");
        var stranger = await h.UserAsync("stranger@test");

        var family = new ParentAccount { UserId = mom.Id, FirstName = "Mom", LastName = "Lopez" };
        h.Db.ParentAccounts.Add(family);
        await h.Db.SaveChangesAsync();
        h.Db.Players.AddRange(
            new Player { ParentAccountId = family.Id, FirstName = "Ana", LastName = "Lopez", DateOfBirth = new DateOnly(2016, 1, 1) },
            new Player { ParentAccountId = family.Id, FirstName = "Luis", LastName = "Lopez", DateOfBirth = new DateOnly(2018, 1, 1) });
        h.Db.ParentAccountCollaborators.AddRange(
            new ParentAccountCollaborator { ParentAccountId = family.Id, UserId = dad.Id, AccessLevel = FamilyAccessLevel.Guardian },
            new ParentAccountCollaborator { ParentAccountId = family.Id, UserId = grandma.Id, AccessLevel = FamilyAccessLevel.Viewer });
        await h.Db.SaveChangesAsync();

        var result = await new AdminReportsController(h.Db).MobileUsage(default);
        var rows = Assert.IsAssignableFrom<IEnumerable<MobileUsageRow>>(Assert.IsType<OkObjectResult>(result.Result).Value)
            .ToDictionary(r => r.Email, r => r.PlayerCount);

        Assert.Equal(2, rows["mom@test"]);
        Assert.Equal(2, rows["dad@test"]);      // linked guardian
        Assert.Equal(2, rows["grandma@test"]);  // linked view-only: still sees the kids in the app
        Assert.Equal(0, rows["stranger@test"]);
    }
}
