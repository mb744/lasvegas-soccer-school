using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SoccerSchool.Api.Data;
using SoccerSchool.Api.Domain;
using SoccerSchool.Api.Services;

namespace SoccerSchool.Api.Controllers.Mobile;

/// <summary>
/// What the parent app compares its own version against on launch: the version live in the store
/// ("a new version is available") and the admin-set minimum ("you must update"). Anonymous so the
/// check also works on the sign-in screen.
/// </summary>
[ApiController]
[Route("api/mobile/app-version")]
[AllowAnonymous]
public class MobileAppVersionController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IAppStoreVersionService _store;

    public MobileAppVersionController(AppDbContext db, IAppStoreVersionService store)
    {
        _db = db;
        _store = store;
    }

    [HttpGet]
    public async Task<ActionResult<MobileAppVersionDto>> Get([FromQuery] string? platform, CancellationToken ct)
    {
        var minimum = await _db.MobileAppSettings.AsNoTracking().Select(s => s.MinimumVersion).FirstOrDefaultAsync(ct);

        // The Android app isn't in Google Play yet: no store version or link to send them to.
        if (string.Equals(platform, "android", StringComparison.OrdinalIgnoreCase))
            return Ok(new MobileAppVersionDto(null, minimum, null));

        var release = await _store.GetIosReleaseAsync(ct);
        return Ok(new MobileAppVersionDto(
            release?.Version, minimum, release?.Url ?? AppStoreVersionService.IosStoreUrl));
    }
}

/// <summary>Admin → Settings → Mobile app: the forced-update minimum version.</summary>
[ApiController]
[Route("api/admin/mobile-app-settings")]
[Authorize(AuthenticationSchemes = AuthSchemes.CookieOrMobileJwt)]
[RequirePermission(Permissions.SettingsManage)]
public class AdminMobileAppSettingsController : ControllerBase
{
    private static readonly Regex VersionPattern = new(@"^\d{1,4}(\.\d{1,4}){0,2}$", RegexOptions.Compiled);

    private readonly AppDbContext _db;
    private readonly IAppStoreVersionService _store;

    public AdminMobileAppSettingsController(AppDbContext db, IAppStoreVersionService store)
    {
        _db = db;
        _store = store;
    }

    [HttpGet]
    public async Task<ActionResult<MobileAppSettingsDto>> Get(CancellationToken ct)
    {
        var s = await _db.MobileAppSettings.AsNoTracking().FirstOrDefaultAsync(ct);
        var release = await _store.GetIosReleaseAsync(ct);
        return Ok(new MobileAppSettingsDto(s?.MinimumVersion, release?.Version, s?.UpdatedAt));
    }

    [HttpPut]
    public async Task<ActionResult<MobileAppSettingsDto>> Put([FromBody] SaveMobileAppSettingsRequest req, CancellationToken ct)
    {
        var minimum = string.IsNullOrWhiteSpace(req.MinimumVersion) ? null : req.MinimumVersion.Trim();
        if (minimum is not null && !VersionPattern.IsMatch(minimum))
            return BadRequest("Use a version like 1.4.0, or leave it empty for no minimum.");

        var s = await _db.MobileAppSettings.FirstOrDefaultAsync(ct);
        if (s is null)
        {
            s = new MobileAppSettings { Id = 1 };
            _db.MobileAppSettings.Add(s);
        }
        s.MinimumVersion = minimum;
        s.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        return await Get(ct);
    }
}

/// <param name="LatestVersion">Live store version; null when unknown (or no store listing).</param>
/// <param name="MinimumVersion">Oldest allowed version; null = no minimum.</param>
/// <param name="StoreUrl">Where the Update button goes; null when there's no store listing.</param>
public record MobileAppVersionDto(string? LatestVersion, string? MinimumVersion, string? StoreUrl);

public record MobileAppSettingsDto(string? MinimumVersion, string? AppStoreVersion, DateTime? UpdatedAt);

public record SaveMobileAppSettingsRequest
{
    [MaxLength(32)]
    public string? MinimumVersion { get; init; }
}
