using System.ComponentModel.DataAnnotations;

namespace SoccerSchool.Api.Domain;

/// <summary>
/// A WhatsApp Content template approved in the Twilio console. Required for business-initiated
/// WhatsApp messages outside the 24-hour customer-service window — free-form body text is rejected
/// by WhatsApp's policy in that case. Sent via Twilio's Content API by passing ContentSid +
/// ContentVariables instead of Body to MessageResource.CreateAsync.
/// </summary>
public class WhatsAppTemplate
{
    public int Id { get; set; }

    /// <summary>Friendly name matching the template name in the Twilio console, e.g. "practice_today".</summary>
    [Required, MaxLength(128)]
    public string Name { get; set; } = string.Empty;

    /// <summary>Twilio Content SID (HX...).</summary>
    [Required, MaxLength(64)]
    public string ContentSid { get; set; } = string.Empty;

    public Language Language { get; set; } = Language.English;

    [MaxLength(512)]
    public string? Description { get; set; }

    /// <summary>Optional copy of the approved template body for the admin's reference, with
    /// placeholders like "{{1}}". Display-only — not sent to Twilio.</summary>
    [MaxLength(2000)]
    public string? PreviewText { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public List<WhatsAppTemplateVariable> Variables { get; set; } = new();
}
