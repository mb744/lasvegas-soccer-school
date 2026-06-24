using System.ComponentModel.DataAnnotations;

namespace SoccerSchool.Api.Domain;

/// <summary>
/// Admin-curated email template. Unlike WhatsApp templates these aren't approved by a third
/// party — they're our own subject + body strings with positional placeholders ({{1}}, {{2}}).
/// At send time we substitute the values directly. Email body is plain text; we wrap it in a
/// minimal HTML container for clients that prefer HTML rendering.
/// </summary>
public class EmailTemplate
{
    public int Id { get; set; }

    [Required, MaxLength(128)]
    public string Name { get; set; } = string.Empty;

    public Language Language { get; set; } = Language.English;

    [MaxLength(512)]
    public string? Description { get; set; }

    [Required, MaxLength(256)]
    public string Subject { get; set; } = string.Empty;

    [Required, MaxLength(8000)]
    public string Body { get; set; } = string.Empty;

    /// <summary>Which send pipeline this template is for. Drives the property registry shown
    /// in the admin's "Map to" dropdown and the send-time resolver that auto-fills mapped
    /// variables from the broadcast's context (event, tournament, month, etc.).
    /// <see cref="TemplateContext.FreeForm"/> is the legacy default — variables stay
    /// positional and the admin types every value by hand.</summary>
    public TemplateContext Context { get; set; } = TemplateContext.FreeForm;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public List<EmailTemplateVariable> Variables { get; set; } = new();
}
