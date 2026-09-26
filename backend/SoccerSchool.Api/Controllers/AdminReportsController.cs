using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SoccerSchool.Api.Data;
using SoccerSchool.Api.Domain;
using SoccerSchool.Api.Dtos;

namespace SoccerSchool.Api.Controllers;

/// <summary>
/// Admin-facing reports (read-only aggregates the ops team wants at a glance). Kept out of the
/// per-domain controllers because the queries fan across several tables and don't fit any single
/// domain's edit surface.
/// </summary>
[ApiController]
[Route("api/admin/reports")]
[Authorize(Roles = Roles.Admin)]
public class AdminReportsController : ControllerBase
{
    private readonly AppDbContext _db;

    public AdminReportsController(AppDbContext db)
    {
        _db = db;
    }

    /// <summary>Every parent-side login with a signal on mobile-app usage. "Has the app" means
    /// any of: an install check-in (the app sends one on every signed-in launch, notifications or
    /// not), a push token, or a mobile session (older app versions that don't check in). Push
    /// status is reported separately so parents who declined notifications still show up. Rows
    /// are ordered "most recently seen" first so admins can scan for who's active.</summary>
    [HttpGet("mobile-usage")]
    public async Task<ActionResult<IEnumerable<MobileUsageRow>>> MobileUsage(CancellationToken ct)
    {
        // One row per user — LEFT JOIN pattern via correlated subqueries so users with zero
        // devices / zero mobile logins still appear (we want the "installed the app? no" case).
        var rows = await _db.Users
            .Select(u => new
            {
                u.Id,
                u.Email,
                u.LastLoginAt,
                Account = _db.ParentAccounts.Where(p => p.UserId == u.Id)
                    .Select(p => new { p.FirstName, p.LastName, p.CreatedAt })
                    .FirstOrDefault(),
                PlayerCount = _db.Players.Count(p => p.ParentAccount != null && p.ParentAccount.UserId == u.Id),
                DeviceCount = _db.Set<DeviceToken>().Count(d => d.UserId == u.Id),
                FirstInstalledAt = _db.Set<DeviceToken>()
                    .Where(d => d.UserId == u.Id)
                    .Min(d => (DateTime?)d.CreatedAt),
                LastSeenAt = _db.Set<DeviceToken>()
                    .Where(d => d.UserId == u.Id)
                    .Max(d => (DateTime?)d.LastSeenAt),
                HasIos = _db.Set<DeviceToken>().Any(d => d.UserId == u.Id && d.Platform == DevicePlatform.Ios)
                    || _db.MobileAppInstalls.Any(i => i.UserId == u.Id && i.Platform == DevicePlatform.Ios),
                HasAndroid = _db.Set<DeviceToken>().Any(d => d.UserId == u.Id && d.Platform == DevicePlatform.Android)
                    || _db.MobileAppInstalls.Any(i => i.UserId == u.Id && i.Platform == DevicePlatform.Android),
                InstallCount = _db.MobileAppInstalls.Count(i => i.UserId == u.Id),
                FirstCheckInAt = _db.MobileAppInstalls.Where(i => i.UserId == u.Id).Min(i => (DateTime?)i.FirstSeenAt),
                LastCheckInAt = _db.MobileAppInstalls.Where(i => i.UserId == u.Id).Max(i => (DateTime?)i.LastSeenAt),
                LatestInstall = _db.MobileAppInstalls.Where(i => i.UserId == u.Id)
                    .OrderByDescending(i => i.LastSeenAt)
                    .Select(i => new { i.AppVersion, i.BuildNumber, i.PushPermission, i.PushError })
                    .FirstOrDefault(),
                LastMobileLoginAt = _db.Set<MobileRefreshToken>()
                    .Where(m => m.UserId == u.Id)
                    .Max(m => (DateTime?)m.CreatedAt),
            })
            .ToListAsync(ct);

        static DateTime? Earliest(params DateTime?[] values) => values.Where(v => v.HasValue).Min();
        static DateTime? Latest(params DateTime?[] values) => values.Where(v => v.HasValue).Max();

        var result = rows
            .Select(r => new MobileUsageRow(
                r.Id,
                r.Email ?? "",
                string.IsNullOrWhiteSpace(r.Account?.FirstName) && string.IsNullOrWhiteSpace(r.Account?.LastName)
                    ? (r.Email ?? "")
                    : $"{r.Account?.FirstName} {r.Account?.LastName}".Trim(),
                r.PlayerCount,
                r.InstallCount > 0 || r.DeviceCount > 0 || r.LastMobileLoginAt != null,
                Earliest(r.FirstCheckInAt, r.FirstInstalledAt),
                Latest(r.LastCheckInAt, r.LastSeenAt, r.LastMobileLoginAt),
                r.LastMobileLoginAt,
                r.HasIos,
                r.HasAndroid,
                Math.Max(r.DeviceCount, r.InstallCount),
                r.LastLoginAt,
                r.Account?.CreatedAt,
                r.DeviceCount > 0,
                r.LatestInstall?.PushPermission,
                r.LatestInstall?.PushError,
                r.LatestInstall?.AppVersion is null
                    ? null
                    : string.IsNullOrEmpty(r.LatestInstall.BuildNumber)
                        ? r.LatestInstall.AppVersion
                        : $"{r.LatestInstall.AppVersion} ({r.LatestInstall.BuildNumber})"))
            // Active mobile users first (most recent LastSeenAt), then the never-installed pile.
            .OrderByDescending(r => r.HasMobileApp)
            .ThenByDescending(r => r.LastSeenAt ?? DateTime.MinValue)
            .ToList();

        return Ok(result);
    }
}
