using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SoccerSchool.Api.Data;
using SoccerSchool.Api.Domain;
using SoccerSchool.Api.Dtos;

namespace SoccerSchool.Api.Controllers;

/// <summary>
/// Admin CRUD for the DOB → age-bracket mapping used at registration time. List is also exposed
/// to non-admin callers so the registration UI can preview the classification a DOB will produce
/// — that helps parents and admins double-check the bracket before submit.
/// </summary>
[ApiController]
[Route("api/age-classifications")]
public class AgeClassificationsController : ControllerBase
{
    private readonly AppDbContext _db;

    public AgeClassificationsController(AppDbContext db) { _db = db; }

    [HttpGet]
    [Authorize]
    public async Task<ActionResult<IEnumerable<AgeClassificationDto>>> List(CancellationToken ct)
    {
        var items = await _db.AgeClassifications
            .OrderBy(c => c.DobStart)
            .Select(c => new AgeClassificationDto(
                c.Id, c.Name, c.Description, c.DobStart, c.DobEnd, c.CreatedAt, c.UpdatedAt))
            .ToListAsync(ct);
        return Ok(items);
    }

    [HttpPost]
    [Authorize(Roles = Roles.Admin)]
    public async Task<ActionResult<AgeClassificationDto>> Create(
        [FromBody] SaveAgeClassificationRequest request, CancellationToken ct)
    {
        var error = Validate(request);
        if (error is not null) return BadRequest(error);

        var name = request.Name.Trim();
        if (await _db.AgeClassifications.AnyAsync(c => c.Name == name, ct))
            return Conflict($"A classification named '{name}' already exists.");

        var c = new AgeClassification
        {
            Name = name,
            Description = request.Description?.Trim(),
            DobStart = request.DobStart,
            DobEnd = request.DobEnd,
        };
        _db.AgeClassifications.Add(c);
        await _db.SaveChangesAsync(ct);
        return Ok(ToDto(c));
    }

    [HttpPut("{id:int}")]
    [Authorize(Roles = Roles.Admin)]
    public async Task<ActionResult<AgeClassificationDto>> Update(
        int id, [FromBody] SaveAgeClassificationRequest request, CancellationToken ct)
    {
        var c = await _db.AgeClassifications.FindAsync(new object?[] { id }, ct);
        if (c is null) return NotFound();

        var error = Validate(request);
        if (error is not null) return BadRequest(error);

        var name = request.Name.Trim();
        if (await _db.AgeClassifications.AnyAsync(x => x.Name == name && x.Id != id, ct))
            return Conflict($"A classification named '{name}' already exists.");

        c.Name = name;
        c.Description = request.Description?.Trim();
        c.DobStart = request.DobStart;
        c.DobEnd = request.DobEnd;
        c.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        return Ok(ToDto(c));
    }

    [HttpDelete("{id:int}")]
    [Authorize(Roles = Roles.Admin)]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        var c = await _db.AgeClassifications.FindAsync(new object?[] { id }, ct);
        if (c is null) return NotFound();
        _db.AgeClassifications.Remove(c);
        await _db.SaveChangesAsync(ct);
        return NoContent();
    }

    private static string? Validate(SaveAgeClassificationRequest r)
    {
        if (string.IsNullOrWhiteSpace(r.Name)) return "Name is required.";
        if (r.DobStart == default || r.DobEnd == default) return "DobStart and DobEnd are required.";
        if (r.DobEnd < r.DobStart) return "DobEnd must be on or after DobStart.";
        return null;
    }

    private static AgeClassificationDto ToDto(AgeClassification c) =>
        new(c.Id, c.Name, c.Description, c.DobStart, c.DobEnd, c.CreatedAt, c.UpdatedAt);
}
