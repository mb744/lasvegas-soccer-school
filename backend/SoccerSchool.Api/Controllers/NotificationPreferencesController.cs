using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SoccerSchool.Api.Data;
using SoccerSchool.Api.Domain;
using SoccerSchool.Api.Services;

namespace SoccerSchool.Api.Controllers;

/// <summary>
/// "Update your preferences here" from an event email: no sign-in, the signed link says whose
/// settings these are. GET only reads, so link scanners can't change anything.
/// </summary>
[ApiController]
[Route("api/notification-preferences")]
[AllowAnonymous]
public class NotificationPreferencesController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IPreferenceTokens _tokens;

    public NotificationPreferencesController(AppDbContext db, IPreferenceTokens tokens)
    {
        _db = db;
        _tokens = tokens;
    }

    [HttpGet]
    public async Task<ActionResult<NotificationPreferencesDto>> Get([FromQuery] string t, CancellationToken ct)
    {
        var subject = _tokens.Read(t);
        if (subject is null) return BadRequest("This link isn't valid.");
        var dto = await NotificationPreferencesStore.LoadAsync(_db, subject, ct);
        return dto is null ? BadRequest("This link isn't valid anymore.") : Ok(dto);
    }

    [HttpPut]
    public async Task<ActionResult<NotificationPreferencesDto>> Save([FromBody] SaveNotificationPreferencesByLinkRequest req, CancellationToken ct)
    {
        var subject = _tokens.Read(req.Token);
        if (subject is null) return BadRequest("This link isn't valid.");
        if (!Enum.IsDefined(req.GameEmails) || !Enum.IsDefined(req.EventEmails)) return BadRequest("Unknown option.");
        var dto = await NotificationPreferencesStore.SaveAsync(_db, subject, req.GameEmails, req.EventEmails, req.PushNotifications, ct);
        return dto is null ? BadRequest("This link isn't valid anymore.") : Ok(dto);
    }
}

/// <summary>Profile → Notifications in the app, for the signed-in login.</summary>
[ApiController]
[Route("api/mobile/notification-preferences")]
[Authorize(AuthenticationSchemes = AuthSchemes.MobileJwt)]
public class MobileNotificationPreferencesController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly UserManager<ApplicationUser> _users;

    public MobileNotificationPreferencesController(AppDbContext db, UserManager<ApplicationUser> users)
    {
        _db = db;
        _users = users;
    }

    [HttpGet]
    public async Task<ActionResult<NotificationPreferencesDto>> Get(CancellationToken ct)
    {
        var userId = _users.GetUserId(User);
        if (userId is null) return Unauthorized();
        var dto = await NotificationPreferencesStore.LoadAsync(_db, PreferenceSubject.ForUser(userId), ct);
        return dto is null ? Unauthorized() : Ok(dto);
    }

    [HttpPut]
    public async Task<ActionResult<NotificationPreferencesDto>> Save([FromBody] SaveNotificationPreferencesRequest req, CancellationToken ct)
    {
        var userId = _users.GetUserId(User);
        if (userId is null) return Unauthorized();
        if (!Enum.IsDefined(req.GameEmails) || !Enum.IsDefined(req.EventEmails)) return BadRequest("Unknown option.");
        var dto = await NotificationPreferencesStore.SaveAsync(_db, PreferenceSubject.ForUser(userId),
            req.GameEmails, req.EventEmails, req.PushNotifications, ct);
        return dto is null ? Unauthorized() : Ok(dto);
    }
}

public static class NotificationPreferencesStore
{
    public static async Task<NotificationPreferencesDto?> LoadAsync(AppDbContext db, PreferenceSubject s, CancellationToken ct)
    {
        if (s.UserId is not null)
        {
            var u = await db.Users.AsNoTracking()
                .Where(x => x.Id == s.UserId)
                .Select(x => new
                {
                    x.Email, x.PushMuted, x.GameEmails, x.EventEmails,
                    FirstName = x.ParentAccount != null ? x.ParentAccount.FirstName : null,
                    Platforms = db.DeviceTokens.Where(d => d.UserId == x.Id).Select(d => d.Platform).Distinct().ToList(),
                })
                .FirstOrDefaultAsync(ct);
            if (u is null) return null;
            return new NotificationPreferencesDto(u.FirstName ?? string.Empty, u.Email ?? string.Empty, true,
                !u.PushMuted, u.GameEmails, u.EventEmails,
                u.Platforms.Where(p => p != DevicePlatform.Unknown).Select(p => p.ToString().ToLowerInvariant()).ToList());
        }

        var c = await db.ParentContacts.AsNoTracking().FirstOrDefaultAsync(x => x.Id == s.ContactId, ct);
        if (c is null) return null;
        return new NotificationPreferencesDto(c.FirstName, c.Email ?? string.Empty, false, false, c.GameEmails, c.EventEmails,
            Array.Empty<string>());
    }

    public static async Task<NotificationPreferencesDto?> SaveAsync(AppDbContext db, PreferenceSubject s,
        EmailPreference games, EmailPreference events, bool? push, CancellationToken ct)
    {
        if (s.UserId is not null)
        {
            var u = await db.Users.FirstOrDefaultAsync(x => x.Id == s.UserId, ct);
            if (u is null) return null;
            u.GameEmails = games;
            u.EventEmails = events;
            if (push is bool p) u.PushMuted = !p;
        }
        else
        {
            var c = await db.ParentContacts.FirstOrDefaultAsync(x => x.Id == s.ContactId, ct);
            if (c is null) return null;
            c.GameEmails = games;
            c.EventEmails = events;
        }
        await db.SaveChangesAsync(ct);
        return await LoadAsync(db, s, ct);
    }
}

/// <param name="HasLogin">False for a family contact without an account: no push setting to show.</param>
/// <param name="AppPlatforms">Where this login has the app ("ios", "android"), so the web page can offer to open it.</param>
public record NotificationPreferencesDto(
    string FirstName, string Email, bool HasLogin, bool PushNotifications,
    EmailPreference GameEmails, EmailPreference EventEmails, IReadOnlyList<string> AppPlatforms);

public class SaveNotificationPreferencesRequest
{
    public EmailPreference GameEmails { get; set; }
    public EmailPreference EventEmails { get; set; }
    /// <summary>Null = leave as is.</summary>
    public bool? PushNotifications { get; set; }
}

public class SaveNotificationPreferencesByLinkRequest : SaveNotificationPreferencesRequest
{
    public string Token { get; set; } = string.Empty;
}
