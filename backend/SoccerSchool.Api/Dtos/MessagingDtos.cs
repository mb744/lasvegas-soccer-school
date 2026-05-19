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

    /// <summary>Which language version of a broadcast goes to this group's members. Defaults to English.</summary>
    public Language Language { get; init; } = Language.English;
}

public record MessageGroupSummary(
    int Id,
    string Name,
    string? Description,
    Language Language,
    int MemberCount,
    DateTime CreatedAt);

public record MessageGroupDetail(
    int Id,
    string Name,
    string? Description,
    Language Language,
    DateTime CreatedAt,
    IReadOnlyList<MessageGroupMemberDto> Members);

public record MessageGroupMemberDto(int Id, string? Name, string Phone, Language Language, int? ParentAccountId);

public record AddMessageGroupMemberRequest
{
    [MaxLength(128)]
    public string? Name { get; init; }

    [Required, MaxLength(32)]
    public string Phone { get; init; } = string.Empty;

    /// <summary>Optional. If omitted, the new member inherits the group's default language.</summary>
    public Language? Language { get; init; }

    public int? ParentAccountId { get; init; }
}

public record UpdateMessageGroupMemberLanguageRequest
{
    public Language Language { get; init; } = SoccerSchool.Api.Domain.Language.English;
}

public record DynamicGroupDto(string Key, string Label, int Count);

// --- Broadcasts (fan-out) ---

public enum RecipientTargetKindDto
{
    Individual = 0,
    CustomGroup = 1,
    DynamicGroup = 2,
    AdHocList = 3
}

public record AdHocRecipientDto(string Phone, string? Name);

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

    /// <summary>Used only when Kind = AdHocList. One-off list of phones the admin typed/pasted
    /// at compose time. Not persisted as a group.</summary>
    public List<AdHocRecipientDto>? Recipients { get; init; }
}

public record CreateBroadcastRequest
{
    public MessageChannel Channel { get; init; } = MessageChannel.Sms;

    /// <summary>English version of the free-form body. At least one of BodyEn/BodyEs is required
    /// when WhatsAppTemplateId is null. Each recipient gets the version matching their language;
    /// if their language is missing, falls back to whichever is set.</summary>
    [MaxLength(2000)]
    public string? BodyEn { get; init; }

    /// <summary>Spanish version of the free-form body.</summary>
    [MaxLength(2000)]
    public string? BodyEs { get; init; }

    /// <summary>Language to use for recipients that didn't come with one attached
    /// (ad-hoc list, individual phone, dynamic group). Default English per admin spec.</summary>
    public Language DefaultLanguage { get; init; } = Language.English;

    /// <summary>Use an approved WhatsApp Content template instead of free-form Body. Channel must
    /// be WhatsApp when this is set, and TemplateVariables must cover every variable on the template.</summary>
    public int? WhatsAppTemplateId { get; init; }

    /// <summary>Values for the template's positional variables, keyed by position as string
    /// (e.g. {"1":"5pm","2":"Sunset Park"}). Empty when sending free-form.</summary>
    public Dictionary<string, string>? TemplateVariables { get; init; }

    /// <summary>When this broadcast was triggered by picking an event in Compose, the event's ID.
    /// Stored on the Broadcast row so the cancellation flow can find recipients of past reminders
    /// for a specific event.</summary>
    public int? ScheduledGameId { get; init; }

    public BroadcastTargetDto Target { get; init; } = new();
}

public record BroadcastSummary(
    int Id,
    MessageChannel Channel,
    string? BodyEn,
    string? BodyEs,
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
    Language Language,
    MessageDeliveryStatus Status,
    string? StatusMessage,
    string? TwilioSid);

public record BroadcastDetail(
    int Id,
    MessageChannel Channel,
    string? BodyEn,
    string? BodyEs,
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

/// <summary>Lightweight reference to a template's opposite-language counterpart. WhatsApp templates
/// are language-locked at approval time, so a bilingual broadcast needs two separate templates;
/// we auto-pair them by base name (e.g. <c>practice_or_game</c> ↔ <c>practice_or_game_es</c>).</summary>
public record TemplatePairDto(
    int Id,
    string Name,
    string ContentSid,
    Language Language,
    string? PreviewText,
    IReadOnlyList<WhatsAppTemplateVariableDto> Variables);

public record WhatsAppTemplateDto(
    int Id,
    string Name,
    string ContentSid,
    Language Language,
    string? Description,
    string? PreviewText,
    DateTime CreatedAt,
    IReadOnlyList<WhatsAppTemplateVariableDto> Variables,
    TemplatePairDto? Paired);

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

// --- Phrase translation dictionary ---

public record PhraseTranslationDto(int Id, string English, string Spanish, DateTime CreatedAt, DateTime UpdatedAt);

public record SavePhraseTranslationRequest
{
    [Required, MaxLength(256)]
    public string English { get; init; } = string.Empty;

    [Required, MaxLength(256)]
    public string Spanish { get; init; } = string.Empty;
}

public record TranslateRequest
{
    [Required, MaxLength(2000)]
    public string Text { get; init; } = string.Empty;

    public Language From { get; init; } = Language.English;
    public Language To { get; init; } = Language.Spanish;
}

public record TranslateResponse(string Translated, IReadOnlyList<string> MatchedPhrases, bool FullyTranslated);

// --- Template preview (renders both EN and ES sides for the bilingual confirm modal) ---

public record TemplatePreviewRequest
{
    public int TemplateId { get; init; }
    public Dictionary<string, string> Values { get; init; } = new();
}

public enum TemplatePreviewSource
{
    /// <summary>Rendered from the language-matching approved template.</summary>
    ApprovedTemplate = 0,
    /// <summary>Rendered from the opposite-language approved template with values translated via the dictionary.</summary>
    TranslatedValues = 1,
    /// <summary>The opposite-language template doesn't exist and there's no way to render this side.</summary>
    Unavailable = 2
}

public record TemplatePreviewSide(
    Language Language,
    string TemplateName,
    string? Rendered,
    TemplatePreviewSource Source,
    IReadOnlyDictionary<string, string>? Values);

public record TemplatePreviewResponse(TemplatePreviewSide English, TemplatePreviewSide Spanish);

// --- Inbound replies (received via Twilio webhook) ---

public record InboundMessageDto(
    int Id,
    MessageChannel Channel,
    string FromPhone,
    string? ToPhone,
    string? Body,
    string? TwilioSid,
    DateTime ReceivedAt);
