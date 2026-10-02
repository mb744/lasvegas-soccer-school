using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SoccerSchool.Api.Controllers;
using SoccerSchool.Api.Domain;
using SoccerSchool.Api.Dtos;
using SoccerSchool.Api.Services;

namespace SoccerSchool.Api.Tests;

public class EventUniformTests
{
    [Fact]
    public async Task Practices_and_events_save_the_picked_uniform_and_reject_unknown_ones()
    {
        await using var h = new Harness();
        var admin = await h.UserAsync("admin@test", admin: true);
        var team = new Team { Name = "U11 Red" };
        var blue = new Uniform { Name = "Blue training kit" };
        h.Db.AddRange(team, blue);
        await h.Db.SaveChangesAsync();

        var api = new ScheduleController(h.Db, null!, null!, h.Permissions)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { User = Harness.Principal(admin) } },
        };
        var queue = new EventNotificationQueue();
        var start = DateTime.UtcNow.AddDays(3);

        var practice = Assert.IsType<ScheduledGameDto>(Assert.IsType<OkObjectResult>((await api.CreatePractice(team.Id,
            new SavePracticeRequest { StartsAt = start, UniformId = blue.Id, NotifyParents = false }, queue, default)).Result).Value);
        Assert.Equal(blue.Id, (await h.Db.ScheduledGames.SingleAsync(g => g.Id == practice.Id)).UniformId);

        var misc = Assert.IsType<ScheduledGameDto>(Assert.IsType<OkObjectResult>((await api.CreateMiscEvent(team.Id,
            new SavePracticeRequest { StartsAt = start, Summary = "Photo day", UniformId = blue.Id, NotifyParents = false }, queue, default)).Result).Value);
        Assert.Equal(blue.Id, (await h.Db.ScheduledGames.SingleAsync(g => g.Id == misc.Id)).UniformId);

        // Back to the club default.
        await api.UpdatePractice(practice.Id, new SavePracticeRequest { StartsAt = start, UniformId = null, NotifyParents = false }, queue, default);
        Assert.Null((await h.Db.ScheduledGames.AsNoTracking().SingleAsync(g => g.Id == practice.Id)).UniformId);

        Assert.IsType<BadRequestObjectResult>((await api.CreatePractice(team.Id,
            new SavePracticeRequest { StartsAt = start, UniformId = 99999, NotifyParents = false }, queue, default)).Result);
    }
}
