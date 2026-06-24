using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SoccerSchool.Api.Data;
using SoccerSchool.Api.Domain;
using SoccerSchool.Api.Dtos;

namespace SoccerSchool.Api.Controllers;

/// <summary>
/// Admin Invoices card backend: list / create / update / delete invoices owed by a
/// <see cref="ParentAccount"/>, and a state-machine transition endpoint that stamps
/// SentAt / PaidAt timestamps as the invoice moves through New → Sent → Paid → Closed.
/// Each invoice is a single charge (tournament fee, monthly subscription line, etc.) tied
/// to one parent; recurring subscriptions are still admin-driven for now (one invoice per
/// cycle) until a future auto-generate pass.
/// </summary>
[ApiController]
[Route("api/admin/invoices")]
[Authorize(Roles = Roles.Admin)]
public class AdminInvoicesController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly UserManager<ApplicationUser> _users;

    public AdminInvoicesController(AppDbContext db, UserManager<ApplicationUser> users)
    {
        _db = db;
        _users = users;
    }

    /// <summary>List invoices for the admin table. Optional filters: <paramref name="status"/>
    /// narrows to a single lifecycle state; <paramref name="parentAccountId"/> narrows to one
    /// family; <paramref name="q"/> matches description text or parent name (case-insensitive
    /// substring). Sorted by IssuedAt desc so the newest charges are on top.</summary>
    [HttpGet]
    public async Task<ActionResult<IEnumerable<InvoiceDto>>> List(
        [FromQuery] InvoiceStatus? status,
        [FromQuery] int? parentAccountId,
        [FromQuery] string? q,
        CancellationToken ct)
    {
        var query = _db.Invoices.AsNoTracking().AsQueryable();
        if (status is InvoiceStatus s) query = query.Where(i => i.Status == s);
        if (parentAccountId is int pid) query = query.Where(i => i.ParentAccountId == pid);

        var rows = await query
            .OrderByDescending(i => i.IssuedAt)
            .Select(i => new
            {
                i.Id, i.ParentAccountId,
                ParentFirstName = i.ParentAccount != null ? i.ParentAccount.FirstName : null,
                ParentLastName = i.ParentAccount != null ? i.ParentAccount.LastName : null,
                ParentEmail = i.ParentAccount != null && i.ParentAccount.User != null ? i.ParentAccount.User.Email : null,
                ParentCellPhone = i.ParentAccount != null ? i.ParentAccount.CellPhone : null,
                i.Description, i.Amount, i.Currency, i.Type, i.Status,
                i.IssuedAt, i.DueDate, i.SentAt, i.PaidAt,
                i.PaymentMethod, i.PaymentReference, i.Notes,
                i.ChargeTypeId,
                ChargeTypeName = i.ChargeType != null ? i.ChargeType.Name : null,
                i.CreatedAt, i.UpdatedAt,
            })
            .ToListAsync(ct);

        IEnumerable<InvoiceDto> dtos = rows.Select(r => new InvoiceDto(
            r.Id, r.ParentAccountId,
            ComposeName(r.ParentFirstName, r.ParentLastName),
            r.ParentEmail, r.ParentCellPhone,
            r.Description, r.Amount, r.Currency, r.Type, r.Status,
            r.IssuedAt, r.DueDate, r.SentAt, r.PaidAt,
            r.PaymentMethod, r.PaymentReference, r.Notes,
            r.ChargeTypeId, r.ChargeTypeName,
            r.CreatedAt, r.UpdatedAt));

        if (!string.IsNullOrWhiteSpace(q))
        {
            var lower = q.Trim().ToLowerInvariant();
            dtos = dtos.Where(d =>
                d.Description.ToLowerInvariant().Contains(lower)
                || (d.ParentName ?? string.Empty).ToLowerInvariant().Contains(lower));
        }
        return Ok(dtos.ToList());
    }

    /// <summary>Aggregate counts + outstanding/paid totals shown above the invoices table.
    /// Counts and sums are computed in SQL so they stay accurate against the full set even
    /// when the table view is filtered.</summary>
    [HttpGet("summary")]
    public async Task<ActionResult<InvoiceSummaryDto>> Summary(CancellationToken ct)
    {
        var counts = await _db.Invoices.AsNoTracking()
            .GroupBy(i => i.Status)
            .Select(g => new { Status = g.Key, Count = g.Count(), Sum = g.Sum(i => i.Amount) })
            .ToListAsync(ct);
        int total = counts.Sum(c => c.Count);
        int Count(InvoiceStatus s) => counts.FirstOrDefault(c => c.Status == s)?.Count ?? 0;
        decimal Sum(InvoiceStatus s) => counts.FirstOrDefault(c => c.Status == s)?.Sum ?? 0m;
        return Ok(new InvoiceSummaryDto(
            TotalCount: total,
            NewCount: Count(InvoiceStatus.New),
            SentCount: Count(InvoiceStatus.Sent),
            PaidCount: Count(InvoiceStatus.Paid),
            ClosedCount: Count(InvoiceStatus.Closed),
            // Outstanding = anything not yet Paid or Closed.
            OutstandingAmount: Sum(InvoiceStatus.New) + Sum(InvoiceStatus.Sent),
            PaidAmount: Sum(InvoiceStatus.Paid)));
    }

    [HttpPost]
    public async Task<ActionResult<InvoiceDto>> Create(
        [FromBody] CreateInvoiceRequest req, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.Description))
            return BadRequest("Description is required.");
        if (req.Amount <= 0) return BadRequest("Amount must be greater than zero.");
        if (!await _db.ParentAccounts.AnyAsync(p => p.Id == req.ParentAccountId, ct))
            return BadRequest("Parent account not found.");
        if (req.ChargeTypeId is int ctId && !await _db.ChargeTypes.AnyAsync(c => c.Id == ctId, ct))
            return BadRequest("Charge type not found.");

        var now = DateTime.UtcNow;
        var inv = new Invoice
        {
            ParentAccountId = req.ParentAccountId,
            ChargeTypeId = req.ChargeTypeId,
            Description = req.Description.Trim(),
            Amount = req.Amount,
            Currency = string.IsNullOrWhiteSpace(req.Currency) ? "USD" : req.Currency.Trim().ToUpperInvariant(),
            Type = req.Type,
            Status = InvoiceStatus.New,
            IssuedAt = now,
            DueDate = req.DueDate,
            Notes = string.IsNullOrWhiteSpace(req.Notes) ? null : req.Notes.Trim(),
            CreatedByUserId = _users.GetUserId(User),
            CreatedAt = now,
            UpdatedAt = now,
        };
        _db.Invoices.Add(inv);
        await _db.SaveChangesAsync(ct);
        return Ok(await BuildDtoAsync(inv.Id, ct));
    }

    [HttpPut("{id:int}")]
    public async Task<ActionResult<InvoiceDto>> Update(
        int id, [FromBody] UpdateInvoiceRequest req, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.Description))
            return BadRequest("Description is required.");
        if (req.Amount <= 0) return BadRequest("Amount must be greater than zero.");

        var inv = await _db.Invoices.FirstOrDefaultAsync(i => i.Id == id, ct);
        if (inv is null) return NotFound();
        if (req.ChargeTypeId is int ctId && !await _db.ChargeTypes.AnyAsync(c => c.Id == ctId, ct))
            return BadRequest("Charge type not found.");

        inv.Description = req.Description.Trim();
        inv.Amount = req.Amount;
        inv.Currency = string.IsNullOrWhiteSpace(req.Currency) ? "USD" : req.Currency.Trim().ToUpperInvariant();
        inv.Type = req.Type;
        inv.DueDate = req.DueDate;
        inv.Notes = string.IsNullOrWhiteSpace(req.Notes) ? null : req.Notes.Trim();
        inv.ChargeTypeId = req.ChargeTypeId;
        inv.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        return Ok(await BuildDtoAsync(inv.Id, ct));
    }

    /// <summary>Lifecycle transition. Stamps SentAt / PaidAt as appropriate. Reverting from
    /// Paid back to another status clears PaidAt so audit logs stay honest; same for SentAt
    /// when reverting from Sent. Payment method + reference are only retained when moving
    /// to Paid.</summary>
    [HttpPost("{id:int}/status")]
    public async Task<ActionResult<InvoiceDto>> ChangeStatus(
        int id, [FromBody] ChangeInvoiceStatusRequest req, CancellationToken ct)
    {
        var inv = await _db.Invoices.FirstOrDefaultAsync(i => i.Id == id, ct);
        if (inv is null) return NotFound();

        var now = DateTime.UtcNow;
        inv.Status = req.Status;
        // SentAt: stamp on first transition INTO Sent; preserve a prior stamp on Paid/Closed
        // so we keep the audit trail; clear when reverting back to New.
        if (req.Status == InvoiceStatus.Sent && inv.SentAt is null) inv.SentAt = now;
        if (req.Status == InvoiceStatus.New) inv.SentAt = null;
        // PaidAt + payment fields: only meaningful in Paid state.
        if (req.Status == InvoiceStatus.Paid)
        {
            inv.PaidAt = now;
            inv.PaymentMethod = string.IsNullOrWhiteSpace(req.PaymentMethod) ? inv.PaymentMethod : req.PaymentMethod.Trim();
            inv.PaymentReference = string.IsNullOrWhiteSpace(req.PaymentReference) ? inv.PaymentReference : req.PaymentReference.Trim();
        }
        else if (req.Status != InvoiceStatus.Closed)
        {
            // Reverting away from Paid (e.g., refund posted but charge reissued). Clear payment.
            inv.PaidAt = null;
            inv.PaymentMethod = null;
            inv.PaymentReference = null;
        }
        inv.UpdatedAt = now;
        await _db.SaveChangesAsync(ct);
        return Ok(await BuildDtoAsync(inv.Id, ct));
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        var inv = await _db.Invoices.FirstOrDefaultAsync(i => i.Id == id, ct);
        if (inv is null) return NotFound();
        _db.Invoices.Remove(inv);
        await _db.SaveChangesAsync(ct);
        return NoContent();
    }

    private async Task<InvoiceDto> BuildDtoAsync(int id, CancellationToken ct)
    {
        var r = await _db.Invoices.AsNoTracking()
            .Where(i => i.Id == id)
            .Select(i => new
            {
                i.Id, i.ParentAccountId,
                ParentFirstName = i.ParentAccount != null ? i.ParentAccount.FirstName : null,
                ParentLastName = i.ParentAccount != null ? i.ParentAccount.LastName : null,
                ParentEmail = i.ParentAccount != null && i.ParentAccount.User != null ? i.ParentAccount.User.Email : null,
                ParentCellPhone = i.ParentAccount != null ? i.ParentAccount.CellPhone : null,
                i.Description, i.Amount, i.Currency, i.Type, i.Status,
                i.IssuedAt, i.DueDate, i.SentAt, i.PaidAt,
                i.PaymentMethod, i.PaymentReference, i.Notes,
                i.ChargeTypeId,
                ChargeTypeName = i.ChargeType != null ? i.ChargeType.Name : null,
                i.CreatedAt, i.UpdatedAt,
            })
            .FirstAsync(ct);
        return new InvoiceDto(
            r.Id, r.ParentAccountId,
            ComposeName(r.ParentFirstName, r.ParentLastName),
            r.ParentEmail, r.ParentCellPhone,
            r.Description, r.Amount, r.Currency, r.Type, r.Status,
            r.IssuedAt, r.DueDate, r.SentAt, r.PaidAt,
            r.PaymentMethod, r.PaymentReference, r.Notes,
            r.ChargeTypeId, r.ChargeTypeName,
            r.CreatedAt, r.UpdatedAt);
    }

    private static string? ComposeName(string? first, string? last)
    {
        var name = $"{first ?? string.Empty} {last ?? string.Empty}".Trim();
        return string.IsNullOrWhiteSpace(name) ? null : name;
    }
}
