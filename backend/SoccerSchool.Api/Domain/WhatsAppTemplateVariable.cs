using System.ComponentModel.DataAnnotations;

namespace SoccerSchool.Api.Domain;

/// <summary>
/// One placeholder in a <see cref="WhatsAppTemplate"/>. Position matches the numeric key Twilio
/// expects in ContentVariables (the "{{1}}" in the approved template becomes key "1"), Label drives
/// the admin UI field label, and Example seeds the input as a hint.
/// </summary>
public class WhatsAppTemplateVariable
{
    public int Id { get; set; }

    public int WhatsAppTemplateId { get; set; }
    public WhatsAppTemplate? Template { get; set; }

    /// <summary>1-based; matches the numeric key Twilio expects in ContentVariables.</summary>
    public int Position { get; set; }

    [Required, MaxLength(64)]
    public string Label { get; set; } = string.Empty;

    [MaxLength(256)]
    public string? Example { get; set; }

    /// <summary>Optional mapping to a property in the template's <see cref="TemplateContext"/>
    /// registry (e.g. "tournament.dates", "player.fullName"). When set, the send pipeline pulls
    /// the resolved value from the context's resolver instead of relying on hard-coded variable
    /// positions, so admins can re-order variables in the template body without a code change.</summary>
    [MaxLength(64)]
    public string? PropertyKey { get; set; }
}
