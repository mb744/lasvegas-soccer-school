using System.ComponentModel.DataAnnotations;

namespace SoccerSchool.Api.Domain;

public enum EventEmailKind
{
    /// <summary>Sent when an admin or coach adds an event.</summary>
    Created = 0,
    /// <summary>Sent when a parent-facing detail of an event changes.</summary>
    Updated = 1,
    /// <summary>Sent about a day before the event.</summary>
    Reminder = 2,
}

/// <summary>
/// Admin-edited wording for the automatic event emails (one row per kind + language; no row =
/// the built-in default). Only the text is editable: the event details, the "What changed" list
/// and the attendance buttons are always generated, so a template can't break the answer links.
/// Text may use placeholders like {team.name} (see <c>EventEmailPlaceholders</c>).
/// </summary>
public class EventEmailTemplate
{
    public int Id { get; set; }

    public EventEmailKind Kind { get; set; }

    public Language Language { get; set; }

    [Required, MaxLength(256)]
    public string Subject { get; set; } = string.Empty;

    /// <summary>Shown above the event details (greeting + intro). Plain text; blank lines start a new paragraph.</summary>
    [Required, MaxLength(4000)]
    public string Message { get; set; } = string.Empty;

    /// <summary>Shown under the attendance buttons. Plain text; may be empty.</summary>
    [MaxLength(2000)]
    public string Footer { get; set; } = string.Empty;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>Login name of the admin who last saved it (informational only).</summary>
    [MaxLength(256)]
    public string? UpdatedBy { get; set; }
}
