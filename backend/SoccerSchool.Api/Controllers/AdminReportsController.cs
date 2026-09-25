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

    /// <summary>Every parent-side login with a signal on mobile-app usage. "Has the app" is
    /// derived from a live DeviceToken row (Expo re-registers on every cold-start; tokens Expo
    /// reports as unregistered get deleted server-side, so a present row means the app is
    /// installed today). Mobile-login timestamp comes from the newest issued refresh token — even
    /// revoked/rotated ones are useful because they tell you when they last actually signed in
    /// from the app. Rows are ordered "most recently seen" first so admins can scan for who's
    /// active.</summary>
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
                HasIos = _db.Set<DeviceToken>().Any(d => d.UserId == u.Id && d.Platform == DevicePlatform.Ios),
                HasAndroid = _db.Set<DeviceToken>().Any(d => d.UserId == u.Id && d.Platform == DevicePlatform.Android),
                LastMobileLoginAt = _db.Set<MobileRefreshToken>()
                    .Where(m => m.UserId == u.Id)
                    .Max(m => (DateTime?)m.CreatedAt),
            })
            .ToListAsync(ct);

        var result = rows
            .Select(r => new MobileUsageRow(
                r.Id,
                r.Email ?? "",
                string.IsNullOrWhiteSpace(r.Account?.FirstName) && string.IsNullOrWhiteSpace(r.Account?.LastName)
                    ? (r.Email ?? "")
                    : $"{r.Account?.FirstName} {r.Account?.LastName}".Trim(),
                r.PlayerCount,
                r.DeviceCount > 0,
                r.FirstInstalledAt,
                r.LastSeenAt,
                r.LastMobileLoginAt,
                r.HasIos,
                r.HasAndroid,
                r.DeviceCount,
                r.LastLoginAt,
                r.Account?.CreatedAt))
            // Active mobile users first (most recent LastSeenAt), then the never-installed pile.
            .OrderByDescending(r => r.HasMobileApp)
            .ThenByDescending(r => r.LastSeenAt ?? DateTime.MinValue)
            .ToList();

        return Ok(result);
    }
}
