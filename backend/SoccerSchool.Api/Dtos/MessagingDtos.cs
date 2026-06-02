using System.ComponentModel.DataAnnotations;
using SoccerSchool.Api.Domain;

namespace SoccerSchool.Api.Dtos;

// --- Config ---

public record MessagingConfigDto(bool Sms, bool WhatsApp, bool Email, bool Conversations);

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

public record MessageGroupMemberDto(int Id, string? Name, string Phone, string? Email, Language Language, int? ParentAccountId);

public record AddMessageGroupMemberRequest
{
    [MaxLength(128)]
    public string? Name { get; init; }

    [Required, MaxLength(32)]
    public string Phone { get; init; } = string.Empty;

    [MaxLength(256)]
    public string? Email { get; init; }

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

public record AdHocRecipientDto(string Phone, string? Name, string? Email = null);

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

    /// <summary>Email-channel subject (EN). Required when Channel = Email and not using EmailTemplateId.</summary>
    [MaxLength(256)]
    public string? SubjectEn { get; init; }

    /// <summary>Email-channel subject (ES).</summary>
    [MaxLength(256)]
    public string? SubjectEs { get; init; }

    /// <summary>Use an approved WhatsApp Content template instead of free-form Body. Channel must
    /// be WhatsApp when this is set, and TemplateVariables must cover every variable on the template.</summary>
    public int? WhatsAppTemplateId { get; init; }

    /// <summary>Use an admin-managed Email template instead of free-form subject/body. Channel
    /// must be Email when this is set. Templates supply both Subject and Body templates with
    /// positional placeholders.</summary>
    public int? EmailTemplateId { get; init; }

    /// <summary>Values for the template's positional variables, keyed by position as string
    /// (e.g. {"1":"5pm","2":"Sunset Park"}). Empty when sending free-form.</summary>
    public Dictionary<string, string>? TemplateVariables { get; init; }

    /// <summary>When this broadcast was triggered by picking an event in Compose, the event's ID.
    /// Stored on the Broadcast row so the cancellation flow can find recipients of past reminders
    /// for a specific event.</summary>
    public int? ScheduledGameId { get; init; }

    /// <summary>When this broadcast is a tournament confirmation request (one per rostered
    /// player), the tournament's ID. Stored on the Broadcast so the webhook can route inbound
    /// replies to TournamentAttendance.</summary>
    public int? TournamentId { get; init; }

    /// <summary>When this broadcast targets a single player (per-player tournament confirmation),
    /// the player's ID. Stored so the resend flow can find the last delivery status per player.</summary>
    public int? PlayerId { get; init; }

    public BroadcastTargetDto Target { get; init; } = new();
}

public record BroadcastSummary(
    int Id,
    MessageChannel Channel,
    string? BodyEn,
    string? BodyEs,
    string? SubjectEn,
    string? SubjectEs,
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
    string? Email,
    Language Language,
    MessageDeliveryStatus Status,
    string? StatusMessage,
    string? TwilioSid,
    string? TemplateUsed);

public record BroadcastDetail(
    int Id,
    MessageChannel Channel,
    string? BodyEn,
    string? BodyEs,
    string? SubjectEn,
    string? SubjectEs,
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

public record WhatsAppTemplateVariableDto(int Id, int Position, string Label, string? Example, string? PropertyKey);

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
    TemplateContext Context,
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

    /// <summary>Maps this variable to a property in the parent template's <see cref="TemplateContext"/>.
    /// Pickable keys come from <c>GET /api/messaging/template-properties/{context}</c>.</summary>
    [MaxLength(64)]
    public string? PropertyKey { get; init; }
}

public record TemplatePropertyDto(string Key, string Label);

public record TemplateContextOptionDto(TemplateContext Context, string Label);

// --- Email templates ---

public record EmailTemplateVariableDto(int Id, int Position, string Label, string? Example);

public record EmailTemplateDto(
    int Id,
    string Name,
    Language Language,
    string? Description,
    string Subject,
    string Body,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    IReadOnlyList<EmailTemplateVariableDto> Variables,
    EmailTemplatePairDto? Paired);

/// <summary>Lightweight reference to an email template's opposite-language counterpart, mirroring
/// <see cref="TemplatePairDto"/>. Auto-paired by base name (strip _en/_es/_english/_spanish).</summary>
public record EmailTemplatePairDto(
    int Id,
    string Name,
    Language Language,
    string Subject,
    string Body,
    IReadOnlyList<EmailTemplateVariableDto> Variables);

public record SaveEmailTemplateRequest
{
    [Required, MaxLength(128)]
    public string Name { get; init; } = string.Empty;

    public Language Language { get; init; } = Language.English;

    [MaxLength(512)]
    public string? Description { get; init; }

    [Required, MaxLength(256)]
    public string Subject { get; init; } = string.Empty;

    [Required, MaxLength(8000)]
    public string Body { get; init; } = string.Empty;

    public List<SaveTemplateVariableDto> Variables { get; init; } = new();
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

    public TemplateContext Context { get; init; } = TemplateContext.FreeForm;

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

// --- Per-phone conversation threads (admin reads & replies in-line) ---

/// <summary>Direction of a single entry in a phone-keyed conversation thread.</summary>
public enum ThreadDirection
{
    /// <summary>Message from the parent to us — sourced from <c>InboundMessages</c>.</summary>
    Inbound = 0,
    /// <summary>Message from us to the parent — sourced from a <c>BroadcastRecipient</c> row.</summary>
    Outbound = 1
}

public record ThreadMessageDto(
    ThreadDirection Direction,
    MessageChannel Channel,
    string Body,
    DateTime At,
    MessageDeliveryStatus? Status,
    string? StatusMessage,
    int? BroadcastId);

public record ThreadSummaryDto(
    string Phone,
    string? Name,
    int? ParentAccountId,
    bool ParentRegistered,
    DateTime LastAt,
    string? LastBody,
    ThreadDirection LastDirection,
    int InboundCount,
    int OutboundCount);

public record ThreadDetailDto(
    string Phone,
    string? Name,
    int? ParentAccountId,
    bool ParentRegistered,
    Language? Language,
    IReadOnlyList<ThreadMessageDto> Messages);

public record SendThreadReplyRequest
{
    public MessageChannel Channel { get; init; } = MessageChannel.Sms;

    [Required, MinLength(1), MaxLength(2000)]
    public string Body { get; init; } = string.Empty;
}

// --- Monthly fee notification (admin one-click broadcast) ---

/// <summary>Summary of who the monthly-fee broadcast would hit, returned by the preview endpoint
/// so the admin can sanity-check before firing.</summary>
public record MonthlyFeePreviewDto(
    int RecipientCount,
    int EnglishCount,
    int SpanishCount,
    bool EnglishTemplateConfigured,
    bool SpanishTemplateConfigured,
    IReadOnlyList<WhatsAppTemplateVariableDto> Variables,
    IReadOnlyDictionary<string, string> SuggestedValues,
    string? EnglishTemplateName,
    string? SpanishTemplateName,
    string? EnglishPreviewText,
    string? SpanishPreviewText);

public record SendMonthlyFeeRequest
{
    /// <summary>Optional values for template variables, keyed by position as string ("1", "2", ...).
    /// Empty if both templates have no variables; otherwise must cover every variable on the
    /// English template (the Spanish template is expected to share the same variable shape).</summary>
    public Dictionary<string, string>? TemplateVariables { get; init; }
}

// --- Inbound replies (received via Twilio webhook) ---

// --- Messaging settings (singleton row used by inbound auto-reply) ---

public record MessagingSettingsDto(
    bool AutoReplyEnabled,
    string AutoReplyTextEn,
    string AutoReplyTextEs,
    string? ZellePhone,
    DateTime UpdatedAt);

public record SaveMessagingSettingsRequest
{
    public bool AutoReplyEnabled { get; init; }

    [Required, MaxLength(2000)]
    public string AutoReplyTextEn { get; init; } = string.Empty;

    [Required, MaxLength(2000)]
    public string AutoReplyTextEs { get; init; } = string.Empty;

    [MaxLength(32)]
    public string? ZellePhone { get; init; }
}

public record InboundMessageDto(
    int Id,
    MessageChannel Channel,
    string FromPhone,
    string? ToPhone,
    string? Body,
    string? TwilioSid,
    DateTime ReceivedAt,
    int? BroadcastId,
    string? BroadcastSummary);
