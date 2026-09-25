using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SoccerSchool.Api.Data;
using SoccerSchool.Api.Domain;
using SoccerSchool.Api.Dtos;
using SoccerSchool.Api.Services;

namespace SoccerSchool.Api.Controllers.Mobile;

/// <summary>
/// Two-step photo/video upload: (1) POST declares the file and gets a write-only SAS URL the
/// device uploads to directly, (2) POST /complete has the server check the blob's real size and
/// type. Only completed media can be attached to chat messages or event galleries.
/// </summary>
[ApiController]
[Route("api/mobile/media")]
[Authorize(AuthenticationSchemes = AuthSchemes.MobileJwt)]
public class MobileMediaController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IMediaStorage _storage;
    private readonly UserManager<ApplicationUser> _users;

    public MobileMediaController(AppDbContext db, IMediaStorage storage, UserManager<ApplicationUser> users)
    {
        _db = db;
        _storage = storage;
        _users = users;
    }

    [HttpPost]
    public async Task<ActionResult<MobileCreateUploadResponse>> Create(
        [FromBody] MobileCreateUploadRequest req, CancellationToken ct)
    {
        var userId = _users.GetUserId(User);
        if (string.IsNullOrEmpty(userId)) return Unauthorized();
        if (!_storage.IsAvailable) return StatusCode(StatusCodes.Status503ServiceUnavailable, "Media uploads are not configured.");

        var contentType = req.ContentType.Trim().ToLowerInvariant();
        var error = _storage.ValidateUpload(req.Kind, contentType, req.SizeBytes);
        if (error is not null) return BadRequest(error);

        var asset = new MediaAsset
        {
            BlobName = _storage.NewBlobName(contentType),
            ContentType = contentType,
            Kind = req.Kind,
            SizeBytes = req.SizeBytes,
            UploadedByUserId = userId,
        };
        _db.MediaAssets.Add(asset);
        await _db.SaveChangesAsync(ct);

        var uploadUri = await _storage.GetUploadUriAsync(asset.BlobName, ct);
        return Ok(new MobileCreateUploadResponse(asset.Id, uploadUri.ToString()));
    }

    [HttpPost("{id:int}/complete")]
    public async Task<ActionResult<MobileMediaDto>> Complete(int id, CancellationToken ct)
    {
        var userId = _users.GetUserId(User);
        if (string.IsNullOrEmpty(userId)) return Unauthorized();

        var asset = await _db.MediaAssets.FirstOrDefaultAsync(a => a.Id == id, ct);
        if (asset is null || asset.UploadedByUserId != userId) return NotFound();

        if (asset.Status != MediaStatus.Ready)
        {
            var result = await _storage.VerifyAsync(asset.BlobName, asset.Kind, ct);
            if (!result.Ok)
            {
                _db.MediaAssets.Remove(asset);
                await _db.SaveChangesAsync(ct);
                return BadRequest(result.Error);
            }
            asset.SizeBytes = result.SizeBytes;
            asset.ContentType = result.ContentType;
            asset.Status = MediaStatus.Ready;
            await _db.SaveChangesAsync(ct);
        }

        return Ok(new MobileMediaDto(asset.Id, asset.Kind, asset.ContentType,
            _storage.GetReadUri(asset.BlobName).ToString()));
    }
}
