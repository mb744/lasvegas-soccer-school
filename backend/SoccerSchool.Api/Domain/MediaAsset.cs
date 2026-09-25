using System.ComponentModel.DataAnnotations;

namespace SoccerSchool.Api.Domain;

public enum MediaKind
{
    Image = 0,
    Video = 1,
}

public enum MediaStatus
{
    /// <summary>Upload URL issued; the device hasn't confirmed the blob landed yet.</summary>
    Pending = 0,
    /// <summary>Server verified the blob's real size and type — safe to attach.</summary>
    Ready = 1,
}

/// <summary>
/// A user-uploaded photo or video stored in Azure Blob Storage. The device uploads straight to
/// blob storage with a short-lived write SAS, then confirms; only <see cref="MediaStatus.Ready"/>
/// assets can be attached to a chat message or an event gallery.
/// </summary>
public class MediaAsset
{
    public int Id { get; set; }

    [Required, MaxLength(256)]
    public string BlobName { get; set; } = string.Empty;

    [Required, MaxLength(100)]
    public string ContentType { get; set; } = string.Empty;

    public MediaKind Kind { get; set; }

    public long SizeBytes { get; set; }

    public MediaStatus Status { get; set; } = MediaStatus.Pending;

    [Required, MaxLength(450)]
    public string UploadedByUserId { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
