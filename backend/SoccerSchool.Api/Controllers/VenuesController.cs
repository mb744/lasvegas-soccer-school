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

    // ------------------------------------------------------------
    // Fields (playing surfaces under a venue)
    // ------------------------------------------------------------

    /// <summary>List the fields under a venue. Available to any authenticated caller so event
    /// forms can populate the field picker; auth is at controller level for consistency.</summary>
    [HttpGet("{venueId:int}/fields")]
    public async Task<ActionResult<IEnumerable<VenueFieldDto>>> ListFields(int venueId, CancellationToken ct)
    {
        if (!await _db.Venues.AnyAsync(v => v.Id == venueId, ct)) return NotFound();
        var rows = await _db.VenueFields
            .AsNoTracking()
            .Where(f => f.VenueId == venueId)
            .OrderBy(f => f.Name)
            .Select(f => new VenueFieldDto(f.Id, f.VenueId, f.Name, f.Notes, f.CreatedAt, f.UpdatedAt))
            .ToListAsync(ct);
        return Ok(rows);
    }

    [HttpPost("{venueId:int}/fields")]
    [Authorize(Roles = Roles.Admin)]
    public async Task<ActionResult<VenueFieldDto>> CreateField(
        int venueId, [FromBody] SaveVenueFieldRequest req, CancellationToken ct)
    {
        if (!await _db.Venues.AnyAsync(v => v.Id == venueId, ct)) return NotFound();
        if (string.IsNullOrWhiteSpace(req.Name)) return BadRequest("Field name is required.");
        var name = req.Name.Trim();
        if (await _db.VenueFields.AnyAsync(f => f.VenueId == venueId && f.Name == name, ct))
            return Conflict($"A field named '{name}' already exists on this venue.");

        var now = DateTime.UtcNow;
        var field = new VenueField
        {
            VenueId = venueId,
            Name = name,
            Notes = Clean(req.Notes),
            CreatedAt = now,
            UpdatedAt = now,
        };
        _db.VenueFields.Add(field);
        await _db.SaveChangesAsync(ct);
        return Ok(new VenueFieldDto(field.Id, field.VenueId, field.Name, field.Notes, field.CreatedAt, field.UpdatedAt));
    }

    [HttpPut("{venueId:int}/fields/{fieldId:int}")]
    [Authorize(Roles = Roles.Admin)]
    public async Task<ActionResult<VenueFieldDto>> UpdateField(
        int venueId, int fieldId, [FromBody] SaveVenueFieldRequest req, CancellationToken ct)
    {
        var field = await _db.VenueFields.FirstOrDefaultAsync(f => f.Id == fieldId && f.VenueId == venueId, ct);
        if (field is null) return NotFound();
        if (string.IsNullOrWhiteSpace(req.Name)) return BadRequest("Field name is required.");
        var name = req.Name.Trim();
        if (await _db.VenueFields.AnyAsync(f => f.VenueId == venueId && f.Name == name && f.Id != fieldId, ct))
            return Conflict($"A field named '{name}' already exists on this venue.");

        field.Name = name;
        field.Notes = Clean(req.Notes);
        field.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        return Ok(new VenueFieldDto(field.Id, field.VenueId, field.Name, field.Notes, field.CreatedAt, field.UpdatedAt));
    }

    [HttpDelete("{venueId:int}/fields/{fieldId:int}")]
    [Authorize(Roles = Roles.Admin)]
    public async Task<IActionResult> DeleteField(int venueId, int fieldId, CancellationToken ct)
    {
        var field = await _db.VenueFields.FirstOrDefaultAsync(f => f.Id == fieldId && f.VenueId == venueId, ct);
        if (field is null) return NotFound();
        _db.VenueFields.Remove(field);
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
