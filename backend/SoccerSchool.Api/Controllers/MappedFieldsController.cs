using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SoccerSchool.Api.Data;
using SoccerSchool.Api.Domain;
using SoccerSchool.Api.Dtos;

namespace SoccerSchool.Api.Controllers;

/// <summary>
/// Admin CRUD for composite "mapped fields" (Settings → Mapped fields): admin-defined properties
/// built by concatenating base properties + literal text via {base.key} placeholders. They surface
/// in the template "Map to" dropdown (key "custom.…") and resolve at send time.
/// </summary>
[ApiController]
[Route("api/mapped-fields")]
[Authorize]
public class MappedFieldsController : ControllerBase
{
    private readonly AppDbContext _db;

    public MappedFieldsController(AppDbContext db) { _db = db; }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<MappedFieldDto>>> List(CancellationToken ct)
    {
        var items = await _db.MappedFields
            .OrderBy(m => m.Name)
            .Select(m => new MappedFieldDto(m.Id, m.Name, m.Key, m.Template, m.CreatedAt, m.UpdatedAt))
            .ToListAsync(ct);
        return Ok(items);
    }

    [HttpPost]
    [Authorize(Roles = Roles.Admin)]
    public async Task<ActionResult<MappedFieldDto>> Create(
        [FromBody] SaveMappedFieldRequest request, CancellationToken ct)
    {
        var error = Validate(request);
        if (error is not null) return BadRequest(error);

        var name = request.Name.Trim();
        if (await _db.MappedFields.AnyAsync(m => m.Name == name, ct))
            return Conflict($"A mapped field named '{name}' already exists.");

        var m = new MappedField
        {
            Name = name,
            Key = await GenerateUniqueKeyAsync(name, ct),
            Template = request.Template.Trim(),
        };
        _db.MappedFields.Add(m);
        await _db.SaveChangesAsync(ct);
        return Ok(ToDto(m));
    }

    [HttpPut("{id:int}")]
    [Authorize(Roles = Roles.Admin)]
    public async Task<ActionResult<MappedFieldDto>> Update(
        int id, [FromBody] SaveMappedFieldRequest request, CancellationToken ct)
    {
        var m = await _db.MappedFields.FindAsync(new object?[] { id }, ct);
        if (m is null) return NotFound();

        var error = Validate(request);
        if (error is not null) return BadRequest(error);

        var name = request.Name.Trim();
        if (await _db.MappedFields.AnyAsync(x => x.Name == name && x.Id != id, ct))
            return Conflict($"A mapped field named '{name}' already exists.");

        // Key stays stable so existing variable mappings keep resolving; only name + template change.
        m.Name = name;
        m.Template = request.Template.Trim();
        m.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        return Ok(ToDto(m));
    }

    [HttpDelete("{id:int}")]
    [Authorize(Roles = Roles.Admin)]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        var m = await _db.MappedFields.FindAsync(new object?[] { id }, ct);
        if (m is null) return NotFound();
        _db.MappedFields.Remove(m);
        await _db.SaveChangesAsync(ct);
        return NoContent();
    }

    private static string? Validate(SaveMappedFieldRequest r)
    {
        if (string.IsNullOrWhiteSpace(r.Name)) return "Name is required.";
        if (string.IsNullOrWhiteSpace(r.Template)) return "Definition is required.";
        return null;
    }

    /// <summary>Builds a stable, unique "custom.{slug}" key from the name, appending -N on collision.</summary>
    private async Task<string> GenerateUniqueKeyAsync(string name, CancellationToken ct)
    {
        var slug = Slugify(name);
        var key = $"custom.{slug}";
        var n = 2;
        while (await _db.MappedFields.AnyAsync(m => m.Key == key, ct))
            key = $"custom.{slug}-{n++}";
        return key;
    }

    private static string Slugify(string name)
    {
        var sb = new StringBuilder(name.Length);
        var prevDash = false;
        foreach (var ch in name.Trim().ToLowerInvariant())
        {
            if (char.IsLetterOrDigit(ch)) { sb.Append(ch); prevDash = false; }
            else if (!prevDash) { sb.Append('-'); prevDash = true; }
        }
        var slug = sb.ToString().Trim('-');
        return string.IsNullOrEmpty(slug) ? "field" : slug;
    }

    private static MappedFieldDto ToDto(MappedField m) =>
        new(m.Id, m.Name, m.Key, m.Template, m.CreatedAt, m.UpdatedAt);
}
