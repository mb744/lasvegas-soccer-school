using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SoccerSchool.Api.Data;
using SoccerSchool.Api.Domain;
using SoccerSchool.Api.Dtos;

namespace SoccerSchool.Api.Controllers;

/// <summary>
/// Admin CRUD for venues/parks (Settings → Venues): name, address, playing surface. Events pick a
/// venue via <see cref="ScheduledGame.VenueId"/>; deleting a venue null-outs that link but keeps
/// the event's free-text Location. List is available to any authenticated admin so the event
/// forms can populate their venue picker.
/// </summary>
[ApiController]
[Route("api/venues")]
[Authorize]
public class VenuesController : ControllerBase
{
    private readonly AppDbContext _db;

    public VenuesController(AppDbContext db) { _db = db; }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<VenueDto>>> List(CancellationToken ct)
    {
        var items = await _db.Venues
            .OrderBy(v => v.Name)
            .Select(v => ToDto(v))
            .ToListAsync(ct);
        return Ok(items);
    }

    [HttpPost]
    [Authorize(Roles = Roles.Admin)]
    public async Task<ActionResult<VenueDto>> Create(
        [FromBody] SaveVenueRequest request, CancellationToken ct)
    {
        var error = Validate(request);
        if (error is not null) return BadRequest(error);

        var name = request.Name.Trim();
        if (await _db.Venues.AnyAsync(v => v.Name == name, ct))
            return Conflict($"A venue named '{name}' already exists.");

        var v = new Venue
        {
            Name = name,
            Address = Clean(request.Address),
            Surface = request.Surface,
        };
        _db.Venues.Add(v);
        await _db.SaveChangesAsync(ct);
        return Ok(ToDto(v));
    }

    [HttpPut("{id:int}")]
    [Authorize(Roles = Roles.Admin)]
    public async Task<ActionResult<VenueDto>> Update(
        int id, [FromBody] SaveVenueRequest request, CancellationToken ct)
    {
        var v = await _db.Venues.FindAsync(new object?[] { id }, ct);
        if (v is null) return NotFound();

        var error = Validate(request);
        if (error is not null) return BadRequest(error);

        var name = request.Name.Trim();
        if (await _db.Venues.AnyAsync(x => x.Name == name && x.Id != id, ct))
            return Conflict($"A venue named '{name}' already exists.");

        v.Name = name;
        v.Address = Clean(request.Address);
        v.Surface = request.Surface;
        v.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        return Ok(ToDto(v));
    }

    [HttpDelete("{id:int}")]
    [Authorize(Roles = Roles.Admin)]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        var v = await _db.Venues.FindAsync(new object?[] { id }, ct);
        if (v is null) return NotFound();
        // Events referencing this venue have their FK set null (free-text Location stays) by the DB.
        _db.Venues.Remove(v);
        await _db.SaveChangesAsync(ct);
        return NoContent();
    }

    private static string? Validate(SaveVenueRequest r)
    {
        if (string.IsNullOrWhiteSpace(r.Name)) return "Name is required.";
        if (!Enum.IsDefined(typeof(SurfaceType), r.Surface)) return "Invalid surface type.";
        return null;
    }

    private static string? Clean(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();

    private static VenueDto ToDto(Venue v) =>
        new(v.Id, v.Name, v.Address, v.Surface, v.CreatedAt, v.UpdatedAt);
}
