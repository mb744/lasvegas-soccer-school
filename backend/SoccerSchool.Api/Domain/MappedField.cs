using System.ComponentModel.DataAnnotations;

namespace SoccerSchool.Api.Domain;

/// <summary>
/// An admin-defined composite template property (Settings → Mapped fields). Lets the admin build a
/// new "field" by concatenating base properties and literal text — e.g. a Template of
/// <c>{event.venue}, {event.address} — {event.field}</c> produces a single value that templates can
/// map to. Exposed in the template "Map to" dropdown under <see cref="Key"/> and resolved at send
/// time by substituting each <c>{base.key}</c> placeholder with its resolved value.
/// </summary>
public class MappedField
{
    public int Id { get; set; }

    /// <summary>Display label shown in the "Map to" dropdown (e.g. "Event location — full").</summary>
    [Required, MaxLength(128)]
    public string Name { get; set; } = string.Empty;

    /// <summary>Stable property key used by mapped template variables — always starts with
    /// "custom." (e.g. "custom.event-location-full"). Generated from the name on create; never
    /// changes afterward so existing variable mappings keep resolving.</summary>
    [Required, MaxLength(160)]
    public string Key { get; set; } = string.Empty;

    /// <summary>The composition: free text with <c>{base.key}</c> placeholders referencing base
    /// properties (event.*, tournament.*, player.*, etc.). Unknown/unresolved placeholders render
    /// as empty.</summary>
    [Required, MaxLength(1024)]
    public string Template { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
