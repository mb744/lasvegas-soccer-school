using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using SoccerSchool.Api.Domain;
using SoccerSchool.Api.Services;

namespace SoccerSchool.Api.Tests;

public class EmailDeliveryReportTests
{
    // Shape of a real ACS EmailDeliveryReportReceived event in Event Grid schema.
    private static string Event(string messageId, string status, string? detail = null) => $$"""
        {
          "id": "{{Guid.NewGuid()}}",
          "topic": "/subscriptions/x/resourceGroups/rg/providers/Microsoft.Communication/communicationServices/acs",
          "subject": "sender/info@lasvegassoccerschool.org/message/{{messageId}}",
          "eventType": "Microsoft.Communication.EmailDeliveryReportReceived",
          "data": {
            "sender": "info@lasvegassoccerschool.org",
            "recipient": "parent@test",
            "messageId": "{{messageId}}",
            "status": "{{status}}",
            "deliveryStatusDetails": { "statusMessage": "{{detail ?? ""}}" },
            "deliveryAttemptTimestamp": "2026-09-28T18:00:00Z"
          },
          "dataVersion": "1.0",
          "eventTime": "2026-09-28T18:00:01Z"
        }
        """;

    private static string Base64(string s) => Convert.ToBase64String(Encoding.UTF8.GetBytes(s));

    private static async Task<BroadcastRecipient> RowAsync(Harness h, string sid, string? email = "parent@test")
    {
        var row = new BroadcastRecipient { BroadcastId = 1, Email = email, TwilioSid = sid, Status = MessageDeliveryStatus.Queued };
        h.Db.BroadcastRecipients.Add(row);
        await h.Db.SaveChangesAsync();
        return row;
    }

    private static EmailDeliveryReportProcessor Processor(Harness h) => new(h.Db, NullLogger<EmailDeliveryReportProcessor>.Instance);

    private static async Task<BroadcastRecipient> ReloadAsync(Harness h, int id) =>
        await h.Db.BroadcastRecipients.AsNoTracking().SingleAsync(r => r.Id == id);

    [Fact]
    public async Task Delivered_report_marks_the_recipient_delivered()
    {
        await using var h = new Harness();
        var row = await RowAsync(h, "op-1");

        Assert.True(await Processor(h).ProcessAsync(Base64(Event("op-1", "Delivered")), default));

        var saved = await ReloadAsync(h, row.Id);
        Assert.Equal(MessageDeliveryStatus.Delivered, saved.Status);
        Assert.Null(saved.ErrorCode);
    }

    [Fact]
    public async Task Bounce_marks_undelivered_with_the_reason()
    {
        await using var h = new Harness();
        var row = await RowAsync(h, "op-2");

        // Raw JSON (not base64) is accepted too.
        Assert.True(await Processor(h).ProcessAsync(Event("op-2", "Bounced", "Mailbox does not exist"), default));

        var saved = await ReloadAsync(h, row.Id);
        Assert.Equal(MessageDeliveryStatus.Undelivered, saved.Status);
        Assert.Equal("Bounced: Mailbox does not exist", saved.StatusMessage);
        Assert.Equal("Bounced", saved.ErrorCode);
    }

    [Theory]
    [InlineData("Suppressed")]
    [InlineData("FilteredSpam")]
    [InlineData("Quarantined")]
    [InlineData("Failed")]
    public async Task Other_non_delivery_outcomes_are_undelivered(string status)
    {
        await using var h = new Harness();
        var row = await RowAsync(h, "op-3");
        await Processor(h).ProcessAsync(Base64(Event("op-3", status)), default);
        Assert.Equal(MessageDeliveryStatus.Undelivered, (await ReloadAsync(h, row.Id)).Status);
    }

    [Fact]
    public async Task Non_final_status_and_non_email_rows_are_left_alone()
    {
        await using var h = new Harness();
        var email = await RowAsync(h, "op-4");
        var sms = await RowAsync(h, "op-5", email: null); // an SMS row can't be touched by an email report

        await Processor(h).ProcessAsync(Base64(Event("op-4", "Expanded")), default);
        await Processor(h).ProcessAsync(Base64(Event("op-5", "Delivered")), default);

        Assert.Equal(MessageDeliveryStatus.Queued, (await ReloadAsync(h, email.Id)).Status);
        Assert.Equal(MessageDeliveryStatus.Queued, (await ReloadAsync(h, sms.Id)).Status);
    }

    [Fact]
    public async Task Unreadable_messages_report_false_and_unknown_ids_are_ignored()
    {
        await using var h = new Harness();
        Assert.False(await Processor(h).ProcessAsync("not json at all", default));
        Assert.True(await Processor(h).ProcessAsync(Base64(Event("no-such-op", "Delivered")), default));
    }
}
