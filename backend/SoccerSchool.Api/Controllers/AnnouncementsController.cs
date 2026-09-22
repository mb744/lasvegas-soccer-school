using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SoccerSchool.Api.Data;
using SoccerSchool.Api.Domain;
using SoccerSchool.Api.Dtos;

namespace SoccerSchool.Api.Controllers;

/// <summary>
/// Admin CRUD for the parent-facing Home-tab announcements the mobile app reads via
/// <c>MobileAnnouncementsController</c>. Cookie auth, admin-role-gated like the rest of the
/// admin surface.
/// </summary>
[ApiController]
[Route("api/announcements")]
[Authorize(Roles = Roles.Admin)]
public class AnnouncementsController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly UserManager<ApplicationUser> _users;

    public AnnouncementsController(AppDbContext db, UserManager<ApplicationUser> users)
    {
        _db = db;
        _users = users;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<AnnouncementDto>>> List(CancellationToken ct)
    {
        var rows = await _db.Announcements
            .OrderByDescending(a => a.IsActive)
            .ThenByDescending(a => a.CreatedAt)
            .Select(a => new AnnouncementDto(
                a.Id, a.Title, a.Body, a.TeamId,
                a.Team != null ? a.Team!.Name : null,
                a.EndsAt, a.IsActive, a.CreatedAt, a.UpdatedAt))
            .ToListAsync(ct);
        return Ok(rows);
    }

    [HttpPost]
    public async Task<ActionResult<AnnouncementDto>> Create([FromBody] SaveAnnouncementRequest req, CancellationToken ct)
    {
        var validation = await ValidateAsync(req, ct);
        if (validation is not null) return validation;

        var userId = _users.GetUserId(User);
        var row = new Announcement
        {
            Title = req.Title.Trim(),
            Body = req.Body.Trim(),
            TeamId = req.TeamId,
            EndsAt = req.EndsAt,
            IsActive = req.IsActive,
            CreatedByUserId = userId,
        };
        _db.Announcements.Add(row);
        await _db.SaveChangesAsync(ct);
        return Ok(await Summarize(row.Id, ct));
    }

    [HttpPut("{id:int}")]
    public async Task<ActionResult<AnnouncementDto>> Update(int id, [FromBody] SaveAnnouncementRequest req, CancellationToken ct)
    {
        var row = await _db.Announcements.FirstOrDefaultAsync(a => a.Id == id, ct);
        if (row is null) return NotFound();
        var validation = await ValidateAsync(req, ct);
        if (validation is not null) return validation;

        row.Title = req.Title.Trim();
        row.Body = req.Body.Trim();
        row.TeamId = req.TeamId;
        row.EndsAt = req.EndsAt;
        row.IsActive = req.IsActive;
        row.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        return Ok(await Summarize(id, ct));
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        var row = await _db.Announcements.FirstOrDefaultAsync(a => a.Id == id, ct);
        if (row is null) return NotFound();
        _db.Announcements.Remove(row);
        await _db.SaveChangesAsync(ct);
        return NoContent();
    }

    private async Task<ActionResult?> ValidateAsync(SaveAnnouncementRequest req, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.Title)) return BadRequest("Title is required.");
        if (string.IsNullOrWhiteSpace(req.Body)) return BadRequest("Body is required.");
        if (req.TeamId is int teamId)
        {
            var exists = await _db.Teams.AnyAsync(t => t.Id == teamId, ct);
            if (!exists) return BadRequest("Selected team not found.");
        }
        return null;
    }

    private async Task<AnnouncementDto> Summarize(int id, CancellationToken ct)
    {
        return await _db.Announcements
            .Where(a => a.Id == id)
            .Select(a => new AnnouncementDto(
                a.Id, a.Title, a.Body, a.TeamId,
                a.Team != null ? a.Team!.Name : null,
                a.EndsAt, a.IsActive, a.CreatedAt, a.UpdatedAt))
            .FirstAsync(ct);
    }
}
