using System.ComponentModel.DataAnnotations;

namespace SoccerSchool.Api.Domain;

/// <summary>
/// Admin-curated phrase dictionary used by the Compose tab's bilingual preview. Translation is
/// substring-substitution (longest-match-first), not statistical/ML translation — appropriate
/// for the small, repetitive soccer-school vocabulary (practice, game, opponent, jersey, field,
/// etc.). Anything not in the dictionary falls back to manual edit by the admin.
/// </summary>
public class PhraseTranslation
{
    public int Id { get; set; }

    [Required, MaxLength(256)]
    public string English { get; set; } = string.Empty;

    [Required, MaxLength(256)]
    public string Spanish { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
