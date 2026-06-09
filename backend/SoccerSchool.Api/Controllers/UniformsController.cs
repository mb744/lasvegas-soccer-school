using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SoccerSchool.Api.Data;
using SoccerSchool.Api.Domain;
using SoccerSchool.Api.Dtos;

namespace SoccerSchool.Api.Controllers;

/// <summary>
/// Admin CRUD for club-wide uniforms (Settings → Uniforms). Each uniform has a name, shirt/shorts/
/// sock colors, and an optional Home/Away/Practice designation. At most one uniform may hold each
/// non-None designation; assigning one to a uniform clears it from whatever held it before. Games
/// reference a uniform explicitly, or fall back to the designation that matches their home/away.
/// </summary>
[ApiController]
[Route("api/uniforms")]
[Authorize]
public class UniformsController : ControllerBase
{
    private readonly AppDbContext _db;

    public UniformsController(AppDbContext db) { _db = db; }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<UniformDto>>> List(CancellationToken ct)
    {
        var items = await _db.Uniforms
            .OrderBy(u => u.Designation == UniformDesignation.None) // designated kits first
            .ThenBy(u => u.Designation)
            .ThenBy(u => u.Name)
            .Select(u => ToDto(u))
            .ToListAsync(ct);
        return Ok(items);
    }

    [HttpPost]
    [Authorize(Roles = Roles.Admin)]
    public async Task<ActionResult<UniformDto>> Create(
        [FromBody] SaveUniformRequest request, CancellationToken ct)
    {
        var error = Validate(request);
        if (error is not null) return BadRequest(error);

        var name = request.Name.Trim();
        if (await _db.Uniforms.AnyAsync(u => u.Name == name, ct))
            return Conflict($"A uniform named '{name}' already exists.");

        var u = new Uniform
        {
            Name = name,
            ShirtColor = Clean(request.ShirtColor),
            ShortsColor = Clean(request.ShortsColor),
            SockColor = Clean(request.SockColor),
            Designation = request.Designation,
        };
        await ClearDesignationHolderAsync(request.Designation, excludeId: null, ct);
        _db.Uniforms.Add(u);
        await _db.SaveChangesAsync(ct);
        return Ok(ToDto(u));
    }

    [HttpPut("{id:int}")]
    [Authorize(Roles = Roles.Admin)]
    public async Task<ActionResult<UniformDto>> Update(
        int id, [FromBody] SaveUniformRequest request, CancellationToken ct)
    {
        var u = await _db.Uniforms.FindAsync(new object?[] { id }, ct);
        if (u is null) return NotFound();

        var error = Validate(request);
        if (error is not null) return BadRequest(error);

        var name = request.Name.Trim();
        if (await _db.Uniforms.AnyAsync(x => x.Name == name && x.Id != id, ct))
            return Conflict($"A uniform named '{name}' already exists.");

        await ClearDesignationHolderAsync(request.Designation, excludeId: id, ct);
        u.Name = name;
        u.ShirtColor = Clean(request.ShirtColor);
        u.ShortsColor = Clean(request.ShortsColor);
        u.SockColor = Clean(request.SockColor);
        u.Designation = request.Designation;
        u.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        return Ok(ToDto(u));
    }

    [HttpDelete("{id:int}")]
    [Authorize(Roles = Roles.Admin)]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        var u = await _db.Uniforms.FindAsync(new object?[] { id }, ct);
        if (u is null) return NotFound();
        // Games referencing this uniform have their FK set null (revert to mapping) by the DB.
        _db.Uniforms.Remove(u);
        await _db.SaveChangesAsync(ct);
        return NoContent();
    }

    /// <summary>Enforces one-per-designation: clears the designation from whatever uniform holds it
    /// (other than the one being saved) so the new assignment doesn't trip the filtered unique index.</summary>
    private async Task ClearDesignationHolderAsync(UniformDesignation designation, int? excludeId, CancellationToken ct)
    {
        if (designation == UniformDesignation.None) return;
        var holders = await _db.Uniforms
            .Where(u => u.Designation == designation && (excludeId == null || u.Id != excludeId))
            .ToListAsync(ct);
        foreach (var h in holders)
        {
            h.Designation = UniformDesignation.None;
            h.UpdatedAt = DateTime.UtcNow;
        }
    }

    private static string? Validate(SaveUniformRequest r)
    {
        if (string.IsNullOrWhiteSpace(r.Name)) return "Name is required.";
        if (!Enum.IsDefined(typeof(UniformDesignation), r.Designation)) return "Invalid designation.";
        return null;
    }

    private static string? Clean(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();

    private static UniformDto ToDto(Uniform u) =>
        new(u.Id, u.Name, u.ShirtColor, u.ShortsColor, u.SockColor, u.Designation, u.CreatedAt, u.UpdatedAt);
}
