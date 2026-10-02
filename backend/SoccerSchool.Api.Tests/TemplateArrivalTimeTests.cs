using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using SoccerSchool.Api.Controllers;
using SoccerSchool.Api.Domain;
using SoccerSchool.Api.Dtos;
using SoccerSchool.Api.Options;
using SoccerSchool.Api.Services;

namespace SoccerSchool.Api.Tests;

public class TemplateArrivalTimeTests
{
    [Fact]
    public async Task Event_templates_can_map_the_arrival_and_end_time()
    {
        Assert.Contains(TemplatePropertyRegistry.ForContext(TemplateContext.EventDetails), p => p.Key == "event.arriveTime");
        Assert.Contains(TemplatePropertyRegistry.ForContext(TemplateContext.EventDetails), p => p.Key == "event.endTime");

        await using var h = new Harness();
        var team = new Team { Name = "U11 Red" };
        h.Db.Teams.Add(team);
        await h.Db.SaveChangesAsync();
        // 6:00 PM Pacific (PDT) on Oct 9, 2026; be there 5:30, ends 7:30.
        var start = new DateTime(2026, 10, 10, 1, 0, 0, DateTimeKind.Utc);
        var game = new ScheduledGame
        {
            TeamId = team.Id, Kind = ScheduledEventKind.Game, ExternalUid = "g1", OpponentName = "Rebels",
            StartsAt = start, ArriveAt = start.AddMinutes(-30), EndsAt = start.AddMinutes(90),
        };
        var template = new WhatsAppTemplate
        {
            Name = "gameday_en", ContentSid = "HX1", Language = Language.English, Context = TemplateContext.EventDetails,
            PreviewText = "Kickoff {{1}}. Be there by {{2}}, done by {{3}}.",
            Variables =
            {
                new WhatsAppTemplateVariable { Position = 1, Label = "Kickoff", PropertyKey = "event.time" },
                new WhatsAppTemplateVariable { Position = 2, Label = "Arrive", PropertyKey = "event.arriveTime" },
                new WhatsAppTemplateVariable { Position = 3, Label = "End", PropertyKey = "event.endTime" },
            },
        };
        h.Db.AddRange(game, template);
        await h.Db.SaveChangesAsync();

        var app = Microsoft.Extensions.Options.Options.Create(new AppOptions());
        var api = new MessagingController(h.Db, null!, null!, new RecipientResolver(h.Db, app), null!, new PhraseTranslator(h.Db), null!,
            Microsoft.Extensions.Options.Options.Create(new TwilioOptions()), app, NullLogger<MessagingController>.Instance);

        var preview = Assert.IsType<TemplatePreviewResponse>(Assert.IsType<OkObjectResult>(
            (await api.TemplatePreview(new TemplatePreviewRequest { TemplateId = template.Id, ScheduledGameId = game.Id }, default)).Result).Value);
        Assert.Equal("Kickoff 6:00 PM. Be there by 5:30 PM, done by 7:30 PM.", preview.English.Rendered);
    }
}
