using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SoccerSchool.Api.Data;
using SoccerSchool.Api.Domain;
using SoccerSchool.Api.Dtos;

namespace SoccerSchool.Api.Controllers;

/// <summary>
/// Admin-managed catalog of billable charge types — name, description, default amount,
/// recurrence cadence (OneTime / Hourly / Daily / Weekly / Monthly / Yearly), and an
/// Active flag for soft-disable. Surfaced from the Settings card so admin defines once,
/// uses many times in <see cref="Invoice"/> creation.
/// </summary>
[ApiController]
[Route("api/admin/charge-types")]
[Authorize(Roles = Roles.Admin)]
public class AdminChargeTypesController : ControllerBase
{
    private readonly AppDbContext _db;

    public AdminChargeTypesController(AppDbContext db) => _db = db;

    /// <summary>List all charge types. <paramref name="activeOnly"/>=true (default in the
    /// Invoices add-form picker) filters to the working set; the Settings management
    /// view passes false to also see retired ones.</summary>
    [HttpGet]
    public async Task<ActionResult<IEnumerable<ChargeTypeDto>>> List(
        [FromQuery] bool activeOnly = false, CancellationToken ct = default)
    {
        var q = _db.ChargeTypes.AsNoTracking().AsQueryable();
        if (activeOnly) q = q.Where(c => c.Active);
        var rows = await q
            .OrderBy(c => c.Name)
            .Select(c => new ChargeTypeDto(c.Id, c.Name, c.Description, c.Amount, c.Recurrence,
                c.Active, c.CreatedAt, c.UpdatedAt))
            .ToListAsync(ct);
        return Ok(rows);
    }

    [HttpPost]
    public async Task<ActionResult<ChargeTypeDto>> Create(
        [FromBody] SaveChargeTypeRequest req, CancellationToken ct)
    {
        var validation = Validate(req);
        if (validation is not null) return validation;
        if (await _db.ChargeTypes.AnyAsync(c => c.Name == req.Name.Trim(), ct))
            return Conflict("A charge type with that name already exists.");

        var now = DateTime.UtcNow;
        var c = new ChargeType
        {
            Name = req.Name.Trim(),
            Description = string.IsNullOrWhiteSpace(req.Description) ? null : req.Description.Trim(),
            Amount = req.Amount,
            Recurrence = req.Recurrence,
            Active = req.Active,
            CreatedAt = now,
            UpdatedAt = now,
        };
        _db.ChargeTypes.Add(c);
        await _db.SaveChangesAsync(ct);
        return Ok(new ChargeTypeDto(c.Id, c.Name, c.Description, c.Amount, c.Recurrence, c.Active, c.CreatedAt, c.UpdatedAt));
    }

    [HttpPut("{id:int}")]
    public async Task<ActionResult<ChargeTypeDto>> Update(
        int id, [FromBody] SaveChargeTypeRequest req, CancellationToken ct)
    {
        var validation = Validate(req);
        if (validation is not null) return validation;

        var c = await _db.ChargeTypes.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (c is null) return NotFound();
        var name = req.Name.Trim();
        if (c.Name != name && await _db.ChargeTypes.AnyAsync(x => x.Name == name && x.Id != id, ct))
            return Conflict("A charge type with that name already exists.");

        c.Name = name;
        c.Description = string.IsNullOrWhiteSpace(req.Description) ? null : req.Description.Trim();
        c.Amount = req.Amount;
        c.Recurrence = req.Recurrence;
        c.Active = req.Active;
        c.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        return Ok(new ChargeTypeDto(c.Id, c.Name, c.Description, c.Amount, c.Recurrence, c.Active, c.CreatedAt, c.UpdatedAt));
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        var c = await _db.ChargeTypes.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (c is null) return NotFound();
        // FK on Invoices is SetNull, so deleting a charge type detaches historical invoices
        // (they keep their description + amount snapshot). Safe to hard-delete.
        _db.ChargeTypes.Remove(c);
        await _db.SaveChangesAsync(ct);
        return NoContent();
    }

    private ActionResult? Validate(SaveChargeTypeRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.Name)) return BadRequest("Name is required.");
        if (req.Amount <= 0) return BadRequest("Amount must be greater than zero.");
        return null;
    }
}
