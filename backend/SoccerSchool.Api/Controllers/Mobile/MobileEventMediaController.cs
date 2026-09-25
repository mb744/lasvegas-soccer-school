using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SoccerSchool.Api.Data;
using SoccerSchool.Api.Domain;
using SoccerSchool.Api.Dtos;
using SoccerSchool.Api.Options;
using SoccerSchool.Api.Services;

namespace SoccerSchool.Api.Controllers.Mobile;

/// <summary>
/// Shared photo/video gallery per game or practice. Visible to anyone connected to the event's
/// team: admins, the team's coaches, and families with a player on the roster. Uploaders and
/// staff can delete; any viewer can report (emails the admin) and blocked users' posts are hidden.
/// </summary>
[ApiController]
[Route("api/mobile/events/{eventId:int}/media")]
[Authorize(AuthenticationSchemes = AuthSchemes.MobileJwt)]
public class MobileEventMediaController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IMediaStorage _storage;
    private readonly ICoachScopeService _coaches;
    private readonly IParentAccountResolver _accounts;
    private readonly UserManager<ApplicationUser> _users;
    private readonly IEmailSender _email;
    private readonly AppOptions _app;
    private readonly ILogger<MobileEventMediaController> _logger;

    public MobileEventMediaController(
        AppDbContext db, IMediaStorage storage, ICoachScopeService coaches,
        IParentAccountResolver accounts, UserManager<ApplicationUser> users,
        IEmailSender email, IOptions<AppOptions> app, ILogger<MobileEventMediaController> logger)
    {
        _db = db;
        _storage = storage;
        _coaches = coaches;
        _accounts = accounts;
        _users = users;
        _email = email;
        _app = app.Value;
        _logger = logger;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<MobileEventMediaDto>>> List(int eventId, CancellationToken ct)
    {
        var access = await ResolveAccessAsync(eventId, ct);
        if (access.Result is not null) return access.Result;

        var blocked = await _db.ChatUserBlocks
            .Where(b => b.BlockerUserId == access.UserId)
            .Select(b => b.BlockedUserId)
            .ToListAsync(ct);

        var rows = await _db.EventMedia
            .Where(e => e.ScheduledGameId == eventId && !blocked.Contains(e.UploadedByUserId))
            .OrderByDescending(e => e.CreatedAt)
            .Select(e => new
            {
                e.Id, e.UploadedByUserId, e.UploaderName, e.Caption, e.CreatedAt,
                e.MediaAssetId, e.MediaAsset!.Kind, e.MediaAsset.ContentType, e.MediaAsset.BlobName,
            })
            .ToListAsync(ct);

        return Ok(rows.Select(r => new MobileEventMediaDto(
            r.Id, eventId,
            new MobileMediaDto(r.MediaAssetId, r.Kind, r.ContentType, _storage.GetReadUri(r.BlobName).ToString()),
            r.UploadedByUserId, r.UploaderName, r.Caption, r.CreatedAt,
            CanDelete: access.IsStaff || r.UploadedByUserId == access.UserId)));
    }

    [HttpPost]
    public async Task<ActionResult<MobileEventMediaDto>> Add(
        int eventId, [FromBody] MobileAddEventMediaRequest req, CancellationToken ct)
    {
        var access = await ResolveAccessAsync(eventId, ct);
        if (access.Result is not null) return access.Result;

        var asset = await _db.MediaAssets.FirstOrDefaultAsync(
            a => a.Id == req.MediaId && a.UploadedByUserId == access.UserId && a.Status == MediaStatus.Ready, ct);
        if (asset is null) return BadRequest("Upload not found or not finished uploading.");

        var account = await _accounts.ResolveByUserIdAsync(access.UserId, ct);
        var name = account is null
            ? (await _users.FindByIdAsync(access.UserId))?.Email ?? "Member"
            : $"{account.FirstName} {account.LastName}".Trim();

        var item = new EventMedia
        {
            ScheduledGameId = eventId,
            MediaAssetId = asset.Id,
            UploadedByUserId = access.UserId,
            UploaderName = string.IsNullOrWhiteSpace(name) ? "Member" : name,
            Caption = string.IsNullOrWhiteSpace(req.Caption) ? null : req.Caption.Trim(),
        };
        _db.EventMedia.Add(item);
        await _db.SaveChangesAsync(ct);

        return Ok(new MobileEventMediaDto(
            item.Id, eventId,
            new MobileMediaDto(asset.Id, asset.Kind, asset.ContentType, _storage.GetReadUri(asset.BlobName).ToString()),
            item.UploadedByUserId, item.UploaderName, item.Caption, item.CreatedAt, CanDelete: true));
    }

    [HttpDelete("{mediaItemId:int}")]
    public async Task<IActionResult> Delete(int eventId, int mediaItemId, CancellationToken ct)
    {
        var access = await ResolveAccessAsync(eventId, ct);
        if (access.Result is not null) return access.Result;

        var item = await _db.EventMedia.Include(e => e.MediaAsset)
            .FirstOrDefaultAsync(e => e.Id == mediaItemId && e.ScheduledGameId == eventId, ct);
        if (item is null) return NotFound();
        if (!access.IsStaff && item.UploadedByUserId != access.UserId) return Forbid();

        var asset = item.MediaAsset;
        _db.EventMedia.Remove(item);
        if (asset is not null) _db.MediaAssets.Remove(asset);
        await _db.SaveChangesAsync(ct);
        if (asset is not null) await _storage.DeleteAsync(asset.BlobName, ct);
        return NoContent();
    }

    /// <summary>Flag an item as objectionable (App Store Guideline 1.2). Emails the admin, who can
    /// delete it from the event screen in the app.</summary>
    [HttpPost("{mediaItemId:int}/report")]
    public async Task<IActionResult> Report(int eventId, int mediaItemId, CancellationToken ct)
    {
        var access = await ResolveAccessAsync(eventId, ct);
        if (access.Result is not null) return access.Result;

        var item = await _db.EventMedia.FirstOrDefaultAsync(e => e.Id == mediaItemId && e.ScheduledGameId == eventId, ct);
        if (item is null) return NotFound();

        item.ReportCount++;
        await _db.SaveChangesAsync(ct);

        if (!string.IsNullOrWhiteSpace(_app.Admin.Email))
        {
            var reporter = (await _users.FindByIdAsync(access.UserId))?.Email ?? access.UserId;
            var result = await _email.SendAsync(
                _app.Admin.Email,
                "Reported event photo/video",
                $"{reporter} reported a photo/video posted by {item.UploaderName} on event #{eventId} " +
                $"(item #{item.Id}, {item.ReportCount} report(s) so far).\n\n" +
                "Open the event in the LVSS app as an admin to review and delete it.",
                ct);
            if (!result.Success) _logger.LogWarning("Event media report email failed: {Message}", result.Message);
        }
        return NoContent();
    }

    private record Access(string UserId, bool IsStaff, ActionResult? Result);

    private async Task<Access> ResolveAccessAsync(int eventId, CancellationToken ct)
    {
        var userId = _users.GetUserId(User);
        if (string.IsNullOrEmpty(userId)) return new Access("", false, Unauthorized());
        if (!_storage.IsAvailable)
            return new Access(userId, false, StatusCode(StatusCodes.Status503ServiceUnavailable, "Media uploads are not configured."));

        var teamId = await _db.ScheduledGames.Where(g => g.Id == eventId).Select(g => (int?)g.TeamId).FirstOrDefaultAsync(ct);
        if (teamId is null) return new Access(userId, false, NotFound());

        var scope = await _coaches.GetScopeAsync(User, ct);
        if (scope.IsAdmin || scope.CoachTeamIds.Contains(teamId.Value))
            return new Access(userId, true, null);

        // Families: owned account plus any family they collaborate on (second parents).
        var accountIds = await _db.ParentAccounts.Where(a => a.UserId == userId).Select(a => a.Id)
            .Concat(_db.ParentAccountCollaborators.Where(c => c.UserId == userId).Select(c => c.ParentAccountId))
            .ToListAsync(ct);
        var onRoster = accountIds.Count > 0 && await _db.TeamPlayers.AnyAsync(
            tp => tp.TeamId == teamId && accountIds.Contains(tp.Player!.ParentAccountId), ct);
        return onRoster ? new Access(userId, false, null) : new Access(userId, false, Forbid());
    }
}
