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

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public List<EmailTemplateVariable> Variables { get; set; } = new();
}
