using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using SoccerSchool.Api.Controllers.Mobile;
using SoccerSchool.Api.Domain;
using SoccerSchool.Api.Services;

namespace SoccerSchool.Api.Tests;

public class StaffAttendanceListTests
{
    private static MobileStaffEventController As(Harness h, ApplicationUser user) =>
        new(h.Db, h.Users, h.Services.GetRequiredService<ICoachScopeService>())
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { User = Harness.Principal(user) } },
        };

    [Fact]
    public async Task Coach_gets_each_player_with_their_answer()
    {
        await using var h = new Harness();
        var coach = await h.UserAsync("coach@test");
        var teamId = await h.CoachAsync(coach);
        var family = new ParentAccount { UserId = (await h.UserAsync("p@test")).Id, FirstName = "P", LastName = "Arent" };
        h.Db.ParentAccounts.Add(family);
        var game = new ScheduledGame { TeamId = teamId, StartsAt = DateTime.UtcNow.AddDays(2) };
        h.Db.ScheduledGames.Add(game);
        await h.Db.SaveChangesAsync();

        Player Kid(string first) => new() { ParentAccountId = family.Id, FirstName = first, LastName = "K", DateOfBirth = new DateOnly(2016, 1, 1) };
        var ana = Kid("Ana"); var ben = Kid("Ben"); var cy = Kid("Cy"); var dee = Kid("Dee");
        h.Db.Players.AddRange(ana, ben, cy, dee);
        await h.Db.SaveChangesAsync();
        foreach (var p in new[] { ana, ben, cy, dee }) h.Db.TeamPlayers.Add(new TeamPlayer { TeamId = teamId, PlayerId = p.Id });
        h.Db.EventAttendances.AddRange(
            new EventAttendance { ScheduledGameId = game.Id, PlayerId = ana.Id, Status = AttendanceStatus.Confirmed },
            new EventAttendance { ScheduledGameId = game.Id, PlayerId = ben.Id, Status = AttendanceStatus.Maybe },
            new EventAttendance { ScheduledGameId = game.Id, PlayerId = cy.Id, Status = AttendanceStatus.Declined });
        await h.Db.SaveChangesAsync();

        var result = await As(h, coach).Attendance(game.Id, default);
        var dto = Assert.IsType<MobileStaffAttendanceDto>(Assert.IsType<OkObjectResult>(result.Result).Value);

        Assert.Equal((1, 1, 1, 1), (dto.Going, dto.Maybe, dto.NotGoing, dto.Pending));
        Assert.Equal(
            new[] { ("Ana", AttendanceStatus.Confirmed), ("Ben", AttendanceStatus.Maybe), ("Cy", AttendanceStatus.Declined), ("Dee", AttendanceStatus.Pending) },
            dto.Players!.Select(p => (p.FirstName, p.Status)));

        // Someone who isn't staff on that team can't see the list.
        var outsider = await h.UserAsync("outsider@test");
        Assert.IsType<ForbidResult>((await As(h, outsider).Attendance(game.Id, default)).Result);
    }
}
