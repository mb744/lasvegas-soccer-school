using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SoccerSchool.Api.Data;
using SoccerSchool.Api.Domain;
using SoccerSchool.Api.Dtos;

namespace SoccerSchool.Api.Controllers.Mobile;

/// <summary>Registers/refreshes the calling device's Expo push token so chat + attendance reminders
/// can reach it. Idempotent: re-registering the same token just bumps <c>LastSeenAt</c>.</summary>
[ApiController]
[Route("api/mobile/devices")]
[Authorize(AuthenticationSchemes = AuthSchemes.MobileJwt)]
public class MobileDevicesController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly UserManager<ApplicationUser> _users;

    public MobileDevicesController(AppDbContext db, UserManager<ApplicationUser> users)
    {
        _db = db;
        _users = users;
    }

    [HttpPost]
    public async Task<IActionResult> Register([FromBody] RegisterDeviceRequest req, CancellationToken ct)
    {
        var userId = _users.GetUserId(User);
        if (string.IsNullOrEmpty(userId)) return Unauthorized();
        if (string.IsNullOrWhiteSpace(req.ExpoPushToken)) return BadRequest("Push token is required.");

        await UpsertPushTokenAsync(userId, req.ExpoPushToken.Trim(), req.Platform, ct);
        await _db.SaveChangesAsync(ct);
        return NoContent();
    }

    /// <summary>Called by the app on every signed-in launch, whether or not it got a push token.
    /// Records the install (version, platform, notification permission, push error) so the admin
    /// usage report sees parents who declined notifications too, and refreshes the push token when
    /// there is one.</summary>
    [HttpPost("check-in")]
    public async Task<IActionResult> CheckIn([FromBody] DeviceCheckInRequest req, CancellationToken ct)
    {
        var userId = _users.GetUserId(User);
        if (string.IsNullOrEmpty(userId)) return Unauthorized();
        if (string.IsNullOrWhiteSpace(req.InstallationId)) return BadRequest("Installation id is required.");

        var now = DateTime.UtcNow;
        var installationId = req.InstallationId.Trim();
        var token = string.IsNullOrWhiteSpace(req.ExpoPushToken) ? null : req.ExpoPushToken.Trim();

        var install = await _db.MobileAppInstalls.FirstOrDefaultAsync(i => i.InstallationId == installationId, ct);
        if (install is null)
        {
            install = new MobileAppInstall { InstallationId = installationId, FirstSeenAt = now };
            _db.MobileAppInstalls.Add(install);
        }
        install.UserId = userId;
        install.Platform = req.Platform;
        install.AppVersion = Clean(req.AppVersion);
        install.BuildNumber = Clean(req.BuildNumber);
        install.OsVersion = Clean(req.OsVersion);
        install.PushPermission = Clean(req.PushPermission);
        install.HasPushToken = token is not null;
        install.PushError = token is null ? Clean(req.PushError) : null;
        install.LastSeenAt = now;

        if (token is not null) await UpsertPushTokenAsync(userId, token, req.Platform, ct);
        await _db.SaveChangesAsync(ct);
        return NoContent();
    }

    private async Task UpsertPushTokenAsync(string userId, string token, DevicePlatform platform, CancellationToken ct)
    {
        var existing = await _db.DeviceTokens.FirstOrDefaultAsync(d => d.ExpoPushToken == token, ct);
        if (existing is null)
        {
            _db.DeviceTokens.Add(new DeviceToken
            {
                UserId = userId,
                ExpoPushToken = token,
                Platform = platform,
            });
        }
        else
        {
            // Token can move to a different login (shared device / re-login). Keep ownership current.
            existing.UserId = userId;
            existing.Platform = platform;
            existing.LastSeenAt = DateTime.UtcNow;
        }
    }

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    /// <summary>Unregister on logout so a signed-out device stops receiving the family's pushes.</summary>
    [HttpDelete]
    public async Task<IActionResult> Unregister([FromBody] RegisterDeviceRequest req, CancellationToken ct)
    {
        var token = req.ExpoPushToken?.Trim();
        if (string.IsNullOrWhiteSpace(token)) return NoContent();
        var rows = await _db.DeviceTokens.Where(d => d.ExpoPushToken == token).ToListAsync(ct);
        if (rows.Count > 0)
        {
            _db.DeviceTokens.RemoveRange(rows);
            await _db.SaveChangesAsync(ct);
        }
        return NoContent();
    }
}
