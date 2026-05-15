using Microsoft.Extensions.Options;
using SoccerSchool.Api.Domain;
using SoccerSchool.Api.Options;
using Twilio;
using ConversationResource = Twilio.Rest.Conversations.V1.ConversationResource;
using ConvParticipantResource = Twilio.Rest.Conversations.V1.Conversation.ParticipantResource;
using ConvMessageResource = Twilio.Rest.Conversations.V1.Conversation.MessageResource;

namespace SoccerSchool.Api.Services;

/// <summary>
/// Wraps the Twilio Conversations API for true group chat: one shared thread where every
/// participant can see and reply to all the others. Distinct from <see cref="IMessageSender"/>
/// which fans out separate 1:1 messages.
/// </summary>
public interface IConversationService
{
    bool IsAvailable(MessageChannel channel);
    Task<ConversationCreateResult> CreateAsync(string title, MessageChannel channel, IEnumerable<ConversationParticipantInput> participants, CancellationToken ct);
    Task<ConversationSendResult> SendMessageAsync(string conversationSid, string body, CancellationToken ct);
    Task<bool> RemoveParticipantAsync(string conversationSid, string participantSid, CancellationToken ct);
    Task<bool> DeleteConversationAsync(string conversationSid, CancellationToken ct);
}

public record ConversationParticipantInput(string Phone, string? Name);

public record ConversationCreateResult(
    bool Success,
    string? ConversationSid,
    IReadOnlyList<ParticipantAdded> Participants,
    string? Message);

public record ParticipantAdded(string Phone, string? Name, string? ParticipantSid, string? Error);

public record ConversationSendResult(bool Success, string? MessageSid, string? Message);

public class ConversationService : IConversationService
{
    private readonly TwilioOptions _twilio;
    private readonly ILogger<ConversationService> _logger;

    public ConversationService(IOptions<TwilioOptions> twilio, ILogger<ConversationService> logger)
    {
        _twilio = twilio.Value;
        _logger = logger;
    }

    public bool IsAvailable(MessageChannel channel) => channel switch
    {
        // Conversations participants need a proxy (sender) number on the chosen channel. So the
        // gate is the same as for fan-out sends: SMS proxy for SMS chats, WhatsApp proxy for WhatsApp chats.
        MessageChannel.Sms => _twilio.IsSmsConfigured,
        MessageChannel.WhatsApp => _twilio.IsWhatsAppConfigured,
        _ => false
    };

    public async Task<ConversationCreateResult> CreateAsync(
        string title,
        MessageChannel channel,
        IEnumerable<ConversationParticipantInput> participants,
        CancellationToken ct)
    {
        if (!IsAvailable(channel))
        {
            var key = channel == MessageChannel.WhatsApp ? "Twilio:WhatsAppFromNumber" : "Twilio:SmsFromNumber";
            return new ConversationCreateResult(false, null, Array.Empty<ParticipantAdded>(),
                $"{channel} not configured (set {key}).");
        }

        try
        {
            TwilioClient.Init(_twilio.AccountSid, _twilio.AuthToken);

            var conversation = await ConversationResource.CreateAsync(friendlyName: title);
            var conversationSid = conversation.Sid;

            var proxy = channel == MessageChannel.WhatsApp
                ? $"whatsapp:{_twilio.WhatsAppFromNumber}"
                : _twilio.SmsFromNumber;

            var added = new List<ParticipantAdded>();
            foreach (var p in participants)
            {
                if (string.IsNullOrWhiteSpace(p.Phone))
                {
                    added.Add(new ParticipantAdded(p.Phone ?? "", p.Name, null, "Empty phone."));
                    continue;
                }
                try
                {
                    var address = channel == MessageChannel.WhatsApp
                        ? $"whatsapp:{p.Phone.Trim()}"
                        : p.Phone.Trim();
                    var participant = await ConvParticipantResource.CreateAsync(
                        pathConversationSid: conversationSid,
                        messagingBindingAddress: address,
                        messagingBindingProxyAddress: proxy);
                    added.Add(new ParticipantAdded(p.Phone, p.Name, participant.Sid, null));
                }
                catch (Twilio.Exceptions.ApiException ex)
                {
                    // One bad participant shouldn't kill the whole conversation create.
                    _logger.LogWarning(ex, "Failed to add participant {Phone} to conversation {Sid}", p.Phone, conversationSid);
                    added.Add(new ParticipantAdded(p.Phone, p.Name, null, $"{ex.Code}: {ex.Message}"));
                }
            }

            var ok = added.Count(a => a.ParticipantSid is not null);
            return new ConversationCreateResult(true, conversationSid, added,
                $"Conversation created with {ok}/{added.Count} participants.");
        }
        catch (Twilio.Exceptions.ApiException ex)
        {
            _logger.LogError(ex, "Twilio CreateConversation failed");
            return new ConversationCreateResult(false, null, Array.Empty<ParticipantAdded>(),
                $"Twilio API error: {ex.Code} {ex.Message}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Twilio CreateConversation failed");
            return new ConversationCreateResult(false, null, Array.Empty<ParticipantAdded>(),
                $"Twilio error: {ex.Message}");
        }
    }

    public async Task<ConversationSendResult> SendMessageAsync(string conversationSid, string body, CancellationToken ct)
    {
        try
        {
            TwilioClient.Init(_twilio.AccountSid, _twilio.AuthToken);
            var msg = await ConvMessageResource.CreateAsync(
                pathConversationSid: conversationSid,
                body: body);
            return new ConversationSendResult(true, msg.Sid, "Sent.");
        }
        catch (Twilio.Exceptions.ApiException ex)
        {
            _logger.LogError(ex, "Twilio Conversation message send failed");
            return new ConversationSendResult(false, null, $"Twilio API error: {ex.Code} {ex.Message}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Twilio Conversation message send failed");
            return new ConversationSendResult(false, null, $"Twilio error: {ex.Message}");
        }
    }

    public async Task<bool> RemoveParticipantAsync(string conversationSid, string participantSid, CancellationToken ct)
    {
        try
        {
            TwilioClient.Init(_twilio.AccountSid, _twilio.AuthToken);
            return await ConvParticipantResource.DeleteAsync(
                pathConversationSid: conversationSid,
                pathSid: participantSid);
        }
        catch (Twilio.Exceptions.ApiException ex)
        {
            _logger.LogError(ex, "Twilio RemoveParticipant failed");
            return false;
        }
    }

    public async Task<bool> DeleteConversationAsync(string conversationSid, CancellationToken ct)
    {
        try
        {
            TwilioClient.Init(_twilio.AccountSid, _twilio.AuthToken);
            return await ConversationResource.DeleteAsync(pathSid: conversationSid);
        }
        catch (Twilio.Exceptions.ApiException ex)
        {
            _logger.LogError(ex, "Twilio DeleteConversation failed");
            return false;
        }
    }
}
