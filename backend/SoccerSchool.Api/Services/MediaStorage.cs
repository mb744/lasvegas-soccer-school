using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Sas;
using Microsoft.Extensions.Options;
using SoccerSchool.Api.Domain;
using SoccerSchool.Api.Options;

namespace SoccerSchool.Api.Services;

public record MediaVerifyResult(bool Ok, string? Error, long SizeBytes, string ContentType);

/// <summary>
/// Blob-side half of photo/video uploads. The container is private: devices upload with a
/// short-lived write-only SAS and everyone reads through short-lived read SAS URLs.
/// </summary>
public interface IMediaStorage
{
    bool IsAvailable { get; }
    string? ValidateUpload(MediaKind kind, string contentType, long sizeBytes);
    string NewBlobName(string contentType);
    Task<Uri> GetUploadUriAsync(string blobName, CancellationToken ct);
    Task<MediaVerifyResult> VerifyAsync(string blobName, MediaKind kind, CancellationToken ct);
    Uri GetReadUri(string blobName);
    Task DeleteAsync(string blobName, CancellationToken ct);
}

public class MediaStorage : IMediaStorage
{
    private static readonly Dictionary<string, string> ImageTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        ["image/jpeg"] = ".jpg", ["image/png"] = ".png", ["image/heic"] = ".heic",
        ["image/heif"] = ".heif", ["image/webp"] = ".webp",
    };
    private static readonly Dictionary<string, string> VideoTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        ["video/mp4"] = ".mp4", ["video/quicktime"] = ".mov",
    };

    // 30 minutes covers a 100MB video on a slow sideline cellular connection.
    private static readonly TimeSpan UploadWindow = TimeSpan.FromMinutes(30);
    private static readonly TimeSpan ReadWindow = TimeSpan.FromHours(2);

    private readonly StorageOptions _opts;
    private readonly BlobContainerClient? _container;
    private volatile bool _containerEnsured;

    public MediaStorage(IOptions<StorageOptions> opts)
    {
        _opts = opts.Value;
        if (_opts.IsConfigured)
            _container = new BlobServiceClient(_opts.ConnectionString).GetBlobContainerClient(_opts.MediaContainerName);
    }

    public bool IsAvailable => _container is not null;

    public string? ValidateUpload(MediaKind kind, string contentType, long sizeBytes)
    {
        var allowed = kind == MediaKind.Image ? ImageTypes : VideoTypes;
        if (!allowed.ContainsKey(contentType ?? ""))
            return $"Unsupported {kind.ToString().ToLowerInvariant()} type '{contentType}'.";
        if (sizeBytes <= 0) return "File is empty.";
        var max = MaxBytes(kind);
        if (sizeBytes > max) return $"File is too large (max {max / (1024 * 1024)} MB).";
        return null;
    }

    public string NewBlobName(string contentType)
    {
        var ext = ImageTypes.TryGetValue(contentType, out var i) ? i
            : VideoTypes.TryGetValue(contentType, out var v) ? v : "";
        var now = DateTime.UtcNow;
        return $"{now:yyyy}/{now:MM}/{Guid.NewGuid():N}{ext}";
    }

    public async Task<Uri> GetUploadUriAsync(string blobName, CancellationToken ct)
    {
        var container = RequireContainer();
        await EnsureContainerAsync(container, ct);
        return container.GetBlobClient(blobName).GenerateSasUri(
            BlobSasPermissions.Create | BlobSasPermissions.Write,
            DateTimeOffset.UtcNow.Add(UploadWindow));
    }

    public async Task<MediaVerifyResult> VerifyAsync(string blobName, MediaKind kind, CancellationToken ct)
    {
        var blob = RequireContainer().GetBlobClient(blobName);
        BlobProperties props;
        try
        {
            props = (await blob.GetPropertiesAsync(cancellationToken: ct)).Value;
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            return new MediaVerifyResult(false, "Upload not found.", 0, "");
        }

        // The SAS can't cap size, so enforce the real limits here and discard anything that breaks them.
        var error = ValidateUpload(kind, props.ContentType, props.ContentLength);
        if (error is not null)
        {
            await blob.DeleteIfExistsAsync(cancellationToken: ct);
            return new MediaVerifyResult(false, error, props.ContentLength, props.ContentType ?? "");
        }
        return new MediaVerifyResult(true, null, props.ContentLength, props.ContentType);
    }

    public Uri GetReadUri(string blobName) =>
        RequireContainer().GetBlobClient(blobName)
            .GenerateSasUri(BlobSasPermissions.Read, DateTimeOffset.UtcNow.Add(ReadWindow));

    public async Task DeleteAsync(string blobName, CancellationToken ct) =>
        await RequireContainer().GetBlobClient(blobName).DeleteIfExistsAsync(cancellationToken: ct);

    private long MaxBytes(MediaKind kind) => kind == MediaKind.Image ? _opts.MaxImageBytes : _opts.MaxVideoBytes;

    private BlobContainerClient RequireContainer() =>
        _container ?? throw new InvalidOperationException("Media storage is not configured (Storage:ConnectionString).");

    private async Task EnsureContainerAsync(BlobContainerClient container, CancellationToken ct)
    {
        if (_containerEnsured) return;
        await container.CreateIfNotExistsAsync(PublicAccessType.None, cancellationToken: ct);
        _containerEnsured = true;
    }
}
