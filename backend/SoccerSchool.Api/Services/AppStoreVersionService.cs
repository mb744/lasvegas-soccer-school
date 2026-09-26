using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;

namespace SoccerSchool.Api.Services;

/// <summary>The version of the parent app currently live in the App Store, and its store page.</summary>
public record StoreRelease(string Version, string Url);

public interface IAppStoreVersionService
{
    /// <summary>The live iOS release, or null when the store can't be reached (the app then just
    /// skips the "update available" prompt).</summary>
    Task<StoreRelease?> GetIosReleaseAsync(CancellationToken ct);
}

/// <summary>
/// Asks Apple's public lookup API which version of the app is live, so the "a new version is
/// available" prompt follows App Store releases with no manual step. Cached for an hour.
/// </summary>
public class AppStoreVersionService : IAppStoreVersionService
{
    public const string IosBundleId = "org.lasvegassoccerschool.app";
    public const string IosStoreUrl = "https://apps.apple.com/us/app/id6812537499";

    private static readonly TimeSpan CacheFor = TimeSpan.FromHours(1);
    private static readonly TimeSpan FailureCacheFor = TimeSpan.FromMinutes(5);
    private const string CacheKey = "appstore:ios-release";

    private readonly IHttpClientFactory _http;
    private readonly IMemoryCache _cache;
    private readonly ILogger<AppStoreVersionService> _logger;

    public AppStoreVersionService(IHttpClientFactory http, IMemoryCache cache, ILogger<AppStoreVersionService> logger)
    {
        _http = http;
        _cache = cache;
        _logger = logger;
    }

    public async Task<StoreRelease?> GetIosReleaseAsync(CancellationToken ct)
    {
        if (_cache.TryGetValue(CacheKey, out StoreRelease? cached)) return cached;

        StoreRelease? release = null;
        try
        {
            var client = _http.CreateClient();
            client.Timeout = TimeSpan.FromSeconds(5);
            // Cache-busting query: Apple's lookup endpoint can serve a stale version for a while
            // after a release.
            var url = $"https://itunes.apple.com/lookup?bundleId={IosBundleId}&country=us&t={DateTime.UtcNow.Ticks}";
            using var doc = JsonDocument.Parse(await client.GetStringAsync(url, ct));
            var results = doc.RootElement.GetProperty("results");
            if (results.GetArrayLength() > 0)
            {
                var app = results[0];
                var version = app.GetProperty("version").GetString();
                var page = app.TryGetProperty("trackViewUrl", out var u) ? u.GetString() : null;
                if (!string.IsNullOrWhiteSpace(version)) release = new StoreRelease(version, page ?? IosStoreUrl);
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
        {
            _logger.LogWarning(ex, "App Store version lookup failed.");
        }

        _cache.Set(CacheKey, release, release is null ? FailureCacheFor : CacheFor);
        return release;
    }
}
