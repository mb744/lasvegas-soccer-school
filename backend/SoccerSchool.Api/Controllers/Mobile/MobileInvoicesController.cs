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
/// Parent-facing invoice list + detail for the mobile app. Distinct from
/// <c>AdminInvoicesController</c>, which is the admin CRUD surface: this one is read-only, scoped
/// to the caller's own family, and never returns admin-only fields like <see cref="Invoice.Notes"/>
/// or <see cref="Invoice.CreatedByUserId"/>.
/// </summary>
[ApiController]
[Route("api/mobile/invoices")]
[Authorize(AuthenticationSchemes = AuthSchemes.MobileJwt)]
public class MobileInvoicesController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly UserManager<ApplicationUser> _users;
    private readonly IParentAccountResolver _accounts;

    public MobileInvoicesController(
        AppDbContext db, UserManager<ApplicationUser> users, IParentAccountResolver accounts)
    {
        _db = db;
        _users = users;
        _accounts = accounts;
    }

    /// <summary>
    /// All invoices for the caller's family. Outstanding (New / Sent) come first ordered by
    /// nearest DueDate, then paid/closed history. Payload is intentionally small — detail comes
    /// from the per-invoice endpoint.
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<IEnumerable<MobileInvoiceSummaryDto>>> List(CancellationToken ct)
    {
        var userId = _users.GetUserId(User);
        if (string.IsNullOrEmpty(userId)) return Unauthorized();
        var account = await _accounts.ResolveByUserIdAsync(userId, ct);
        if (account is null) return Ok(Array.Empty<MobileInvoiceSummaryDto>());

        // Fetch, then sort in memory so we can key on (outstanding-first, due-date-ascending).
        var rows = await _db.Invoices
            .Where(i => i.ParentAccountId == account.Id)
            .Select(i => new
            {
                i.Id,
                i.Description,
                i.Amount,
                i.Currency,
                i.DueDate,
                i.Status,
                i.IssuedAt,
                i.PaidAt,
                PlayerName = i.Player != null
                    ? (i.Player!.FirstName + " " + i.Player.LastName).Trim()
                    : null,
                ChargeTypeName = i.ChargeType != null ? i.ChargeType!.Name : null,
            })
            .ToListAsync(ct);

        var ordered = rows
            .OrderBy(r => IsOutstanding((InvoiceStatus)r.Status) ? 0 : 1)
            .ThenBy(r => r.DueDate ?? DateOnly.MaxValue)
            .ThenByDescending(r => r.IssuedAt)
            .Select(r => new MobileInvoiceSummaryDto(
                r.Id,
                r.Description,
                r.Amount,
                r.Currency,
                r.DueDate,
                r.Status,
                r.IssuedAt,
                r.PaidAt,
                r.PlayerName,
                r.ChargeTypeName))
            .ToList();
        return Ok(ordered);
    }

    /// <summary>Single invoice detail — same fields as the summary plus payment info the parent
    /// needs to actually settle it.</summary>
    [HttpGet("{id:int}")]
    public async Task<ActionResult<MobileInvoiceDetailDto>> Get(int id, CancellationToken ct)
    {
        var userId = _users.GetUserId(User);
        if (string.IsNullOrEmpty(userId)) return Unauthorized();
        var account = await _accounts.ResolveByUserIdAsync(userId, ct);
        if (account is null) return NotFound();

        var invoice = await _db.Invoices
            .Where(i => i.Id == id && i.ParentAccountId == account.Id)
            .Select(i => new MobileInvoiceDetailDto(
                i.Id,
                i.Description,
                i.Amount,
                i.Currency,
                i.DueDate,
                i.Status,
                i.IssuedAt,
                i.SentAt,
                i.PaidAt,
                i.PaymentMethod,
                i.PaymentReference,
                i.Player != null
                    ? (i.Player!.FirstName + " " + i.Player.LastName).Trim()
                    : null,
                i.ChargeType != null ? i.ChargeType!.Name : null))
            .FirstOrDefaultAsync(ct);
        return invoice is null ? NotFound() : Ok(invoice);
    }

    private static bool IsOutstanding(InvoiceStatus status) =>
        status == InvoiceStatus.New || status == InvoiceStatus.Sent;
}
