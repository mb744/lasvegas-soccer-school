using System.ComponentModel.DataAnnotations;

namespace SoccerSchool.Api.Domain;

/// <summary>
/// A photo or video posted to a game/practice's shared gallery by a parent, coach, or admin.
/// </summary>
public class EventMedia
{
    public int Id { get; set; }

    public int ScheduledGameId { get; set; }
    public ScheduledGame? ScheduledGame { get; set; }

    public int MediaAssetId { get; set; }
    public MediaAsset? MediaAsset { get; set; }

    [Required, MaxLength(450)]
    public string UploadedByUserId { get; set; } = string.Empty;

    [Required, MaxLength(160)]
    public string UploaderName { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? Caption { get; set; }

    /// <summary>Times this item was reported by viewers. Each report emails the admin.</summary>
    public int ReportCount { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
