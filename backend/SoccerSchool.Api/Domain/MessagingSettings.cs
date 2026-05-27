using System.ComponentModel.DataAnnotations;

namespace SoccerSchool.Api.Domain;

/// <summary>
/// Singleton-row settings for the admin messaging feature. The auto-reply text and toggle live
/// here so the team can tweak them without a redeploy. Convention: <see cref="Id"/> is always
/// <c>1</c>; the row is seeded by migration and the GET endpoint upserts on first access if
/// somehow missing.
/// </summary>
public class MessagingSettings
{
    public int Id { get; set; } = 1;

    /// <summary>When false, inbound replies don't trigger a canned auto-reply.</summary>
    public bool AutoReplyEnabled { get; set; } = true;

    /// <summary>English auto-reply body. Sent to parents matched to an English language preference
    /// (and to anyone whose language we can't determine).</summary>
    [Required, MaxLength(2000)]
    public string AutoReplyTextEn { get; set; } =
        "Thanks for your message! An admin will reply soon. For urgent matters, please call the team. — Las Vegas Soccer School";

    /// <summary>Spanish auto-reply body. Sent to parents matched to a Spanish language preference.</summary>
    [Required, MaxLength(2000)]
    public string AutoReplyTextEs { get; set; } =
        "¡Gracias por escribirnos! Un administrador le responderá pronto. Si es urgente, llame al equipo. — Las Vegas Soccer School";

    /// <summary>Phone number parents should send Zelle payments to. Stored once here so the
    /// monthly-fee notification can fill the template's phone variable automatically. E.164 or
    /// any format the parents can read — we don't normalize here since it's a display string.</summary>
    [MaxLength(32)]
    public string? ZellePhone { get; set; }

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
