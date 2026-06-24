using System.ComponentModel.DataAnnotations;

namespace SoccerSchool.Api.Domain;

/// <summary>
/// One positional placeholder ({{1}}, {{2}}, ...) in an <see cref="EmailTemplate"/>. Mirrors
/// <see cref="WhatsAppTemplateVariable"/> in shape so the admin UI can reuse the same variable-
/// row component.
/// </summary>
public class EmailTemplateVariable
{
    public int Id { get; set; }

    public int EmailTemplateId { get; set; }
    public EmailTemplate? Template { get; set; }

    /// <summary>1-based; matches the numeric placeholder in the template body/subject.</summary>
    public int Position { get; set; }

    [Required, MaxLength(64)]
    public string Label { get; set; } = string.Empty;

    [MaxLength(256)]
    public string? Example { get; set; }

    /// <summary>Optional mapping to a property in the parent template's <see cref="EmailTemplate.Context"/>
    /// registry (e.g. "tournament.dates", "player.fullName"). When set, the bulk-send pipeline
    /// pulls the resolved value from the context's resolver instead of asking the admin to type
    /// it for every send. Mirrors <see cref="WhatsAppTemplateVariable.PropertyKey"/>.</summary>
    [MaxLength(64)]
    public string? PropertyKey { get; set; }
}
