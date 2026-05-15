using System.ComponentModel.DataAnnotations;
using SoccerSchool.Api.Domain;

namespace SoccerSchool.Api.Dtos;

// --- Config ---

public record MessagingConfigDto(bool Sms, bool WhatsApp, bool Conversations);

// --- Curated groups ---

public record SaveMessageGroupRequest
{
    [Required, MaxLength(128)]
    public string Name { get; init; } = string.Empty;

    [MaxLength(512)]
    public string? Description { get; init; }
}

public record MessageGroupSummary(
    int Id,
    string Name,
    string? Description,
    int MemberCount,
    DateTime CreatedAt);

public record MessageGroupDetail(
    int Id,
    string Name,
    string? Description,
    DateTime CreatedAt,
    IReadOnlyList<MessageGroupMemberDto> Members);

public record MessageGroupMemberDto(int Id, string? Name, string Phone, int? ParentAccountId);

public record AddMessageGroupMemberRequest
{
    [MaxLength(128)]
    public string? Name { get; init; }

    [Required, MaxLength(32)]
    public string Phone { get; init; } = string.Empty;

    public int? ParentAccountId { get; init; }
}

public record DynamicGroupDto(string Key, string Label, int Count);

// --- Broadcasts (fan-out) ---

public enum RecipientTargetKindDto
{
    Individual = 0,
    CustomGroup = 1,
    DynamicGroup = 2
}

public record BroadcastTargetDto
{
    public RecipientTargetKindDto Kind { get; init; } = RecipientTargetKindDto.Individual;

    [MaxLength(32)]
    public string? Phone { get; init; }

    [MaxLength(128)]
    public string? Name { get; init; }

    public int? CustomGroupId { get; init; }

    [MaxLength(64)]
    public string? DynamicGroupKey { get; init; }
}

public record CreateBroadcastRequest
{
    public MessageChannel Channel { get; init; } = MessageChannel.Sms;

    /// <summary>Free-form message body. Required when WhatsAppTemplateId is null. For WhatsApp
    /// outside the 24h customer-service window, use the template path instead.</summary>
    [MaxLength(2000)]
    public string? Body { get; init; }

    /// <summary>Use an approved WhatsApp Content template instead of free-form Body. Channel must
    /// be WhatsApp when this is set, and TemplateVariables must cover every variable on the template.</summary>
    public int? WhatsAppTemplateId { get; init; }

    /// <summary>Values for the template's positional variables, keyed by position as string
    /// (e.g. {"1":"5pm","2":"Sunset Park"}). Empty when sending free-form.</summary>
    public Dictionary<string, string>? TemplateVariables { get; init; }

    public BroadcastTargetDto Target { get; init; } = new();
}

public record BroadcastSummary(
    int Id,
    MessageChannel Channel,
    string Body,
    string? TargetLabel,
    DateTime CreatedAt,
    int Total,
    int Queued,
    int Delivered,
    int Failed);

public record BroadcastRecipientDto(
    int Id,
    string? Name,
    string Phone,
    MessageDeliveryStatus Status,
    string? StatusMessage,
    string? TwilioSid);

public record BroadcastDetail(
    int Id,
    MessageChannel Channel,
    string Body,
    string? TargetLabel,
    DateTime CreatedAt,
    IReadOnlyList<BroadcastRecipientDto> Recipients);

// --- Conversations (true group chat) ---

public record ConversationParticipantDto(string Phone, string? Name);

public record CreateGroupConversationRequest
{
    [Required, MaxLength(128)]
    public string Title { get; init; } = string.Empty;

    public MessageChannel Channel { get; init; } = MessageChannel.Sms;

    /// <summary>Explicit participants. Either Participants or Target must be set.</summary>
    public List<ConversationParticipantDto> Participants { get; init; } = new();

    /// <summary>Optional recipient target that expands into participants (e.g. a curated group).</summary>
    public BroadcastTargetDto? Target { get; init; }
}

public record SendGroupConversationRequest
{
    [Required, MinLength(1), MaxLength(2000)]
    public string Body { get; init; } = string.Empty;
}

public record GroupConversationParticipantDto(
    int Id,
    string? Name,
    string Phone,
    string? TwilioParticipantSid);

public record GroupConversationSummary(
    int Id,
    string Title,
    MessageChannel Channel,
    string TwilioConversationSid,
    int ParticipantCount,
    DateTime CreatedAt);

public record GroupConversationDetail(
    int Id,
    string Title,
    MessageChannel Channel,
    string TwilioConversationSid,
    DateTime CreatedAt,
    IReadOnlyList<GroupConversationParticipantDto> Participants);

// --- WhatsApp templates ---

public record WhatsAppTemplateVariableDto(int Id, int Position, string Label, string? Example);

public record WhatsAppTemplateDto(
    int Id,
    string Name,
    string ContentSid,
    Language Language,
    string? Description,
    string? PreviewText,
    DateTime CreatedAt,
    IReadOnlyList<WhatsAppTemplateVariableDto> Variables);

public record SaveTemplateVariableDto
{
    public int Position { get; init; }

    [Required, MaxLength(64)]
    public string Label { get; init; } = string.Empty;

    [MaxLength(256)]
    public string? Example { get; init; }
}

public record SaveWhatsAppTemplateRequest
{
    [Required, MaxLength(128)]
    public string Name { get; init; } = string.Empty;

    [Required, MaxLength(64)]
    public string ContentSid { get; init; } = string.Empty;

    public Language Language { get; init; } = Language.English;

    [MaxLength(512)]
    public string? Description { get; init; }

    [MaxLength(2000)]
    public string? PreviewText { get; init; }

    public List<SaveTemplateVariableDto> Variables { get; init; } = new();
}
