using System.ComponentModel.DataAnnotations;

namespace SoccerSchool.Api.Domain;

/// <summary>
/// A user's flag against a chat message they found objectionable. Reviewed by an admin, who can
/// then delete the message and/or remove the offending sender from the group. Required by App
/// Store Guideline 1.2 for apps that surface user-generated content.
/// </summary>
public class ChatMessageReport
{
    public int Id { get; set; }

    public int ChatMessageId { get; set; }
    public ChatMessage? ChatMessage { get; set; }

    [Required, MaxLength(450)]
    public string ReporterUserId { get; set; } = string.Empty;

    /// <summary>Denormalized so a later user-anonymization on the reporter still preserves the audit
    /// trail with the label they saw on their message list.</summary>
    [Required, MaxLength(160)]
    public string ReporterName { get; set; } = string.Empty;

    /// <summary>Free-form reason the reporter supplied (optional; UI defaults to empty).</summary>
    [MaxLength(1000)]
    public string? Reason { get; set; }

    public DateTime ReportedAt { get; set; } = DateTime.UtcNow;

    /// <summary>UTC an admin resolved the report (removed the message, warned the sender, or
    /// dismissed as OK). Null while pending — used by the admin queue to filter open items.</summary>
    public DateTime? ResolvedAt { get; set; }

    [MaxLength(450)]
    public string? ResolvedByUserId { get; set; }
}
