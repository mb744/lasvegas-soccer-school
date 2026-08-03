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

        var token = req.ExpoPushToken.Trim();
        var existing = await _db.DeviceTokens.FirstOrDefaultAsync(d => d.ExpoPushToken == token, ct);
        if (existing is null)
        {
            _db.DeviceTokens.Add(new DeviceToken
            {
                UserId = userId,
                ExpoPushToken = token,
                Platform = req.Platform,
            });
        }
        else
        {
            // Token can move to a different login (shared device / re-login). Keep ownership current.
            existing.UserId = userId;
            existing.Platform = req.Platform;
            existing.LastSeenAt = DateTime.UtcNow;
        }
        await _db.SaveChangesAsync(ct);
        return NoContent();
    }

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
