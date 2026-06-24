using System.ComponentModel.DataAnnotations;
using SoccerSchool.Api.Domain;

namespace SoccerSchool.Api.Dtos;

/// <summary>One row in the admin Invoices table — invoice fields plus the joined parent
/// contact so the table can show "who owes" without an extra round trip.</summary>
public record InvoiceDto(
    int Id,
    int ParentAccountId,
    string? ParentName,
    string? ParentEmail,
    string? ParentCellPhone,
    string Description,
    decimal Amount,
    string Currency,
    InvoiceType Type,
    InvoiceStatus Status,
    DateTime IssuedAt,
    DateOnly? DueDate,
    DateTime? SentAt,
    DateTime? PaidAt,
    string? PaymentMethod,
    string? PaymentReference,
    string? Notes,
    /// <summary>Optional FK to the <see cref="ChargeType"/> the invoice was created from.
    /// Null for ad-hoc invoices the admin typed in directly.</summary>
    int? ChargeTypeId,
    string? ChargeTypeName,
    DateTime CreatedAt,
    DateTime UpdatedAt);

public record CreateInvoiceRequest
{
    [Required] public int ParentAccountId { get; init; }
    [Required, MaxLength(256)] public string Description { get; init; } = string.Empty;
    [Required, Range(0.01, 1_000_000)] public decimal Amount { get; init; }
    [MaxLength(3)] public string Currency { get; init; } = "USD";
    public InvoiceType Type { get; init; } = InvoiceType.OneTime;
    public DateOnly? DueDate { get; init; }
    [MaxLength(2000)] public string? Notes { get; init; }
    /// <summary>When the admin picked a ChargeType in the create form, store it on the
    /// invoice for later reporting. Validated to exist when set.</summary>
    public int? ChargeTypeId { get; init; }
}

/// <summary>Mutable invoice fields the admin can edit post-create. Status changes go through
/// the dedicated <see cref="ChangeInvoiceStatusRequest"/> endpoint so the timestamp stamps
/// (SentAt / PaidAt) and validation rules stay consistent.</summary>
public record UpdateInvoiceRequest
{
    [Required, MaxLength(256)] public string Description { get; init; } = string.Empty;
    [Required, Range(0.01, 1_000_000)] public decimal Amount { get; init; }
    [MaxLength(3)] public string Currency { get; init; } = "USD";
    public InvoiceType Type { get; init; } = InvoiceType.OneTime;
    public DateOnly? DueDate { get; init; }
    [MaxLength(2000)] public string? Notes { get; init; }
    public int? ChargeTypeId { get; init; }
}

/// <summary>State-machine transition request. PaymentMethod + PaymentReference are admin-
/// entered when moving to Paid; ignored otherwise.</summary>
public record ChangeInvoiceStatusRequest
{
    [Required] public InvoiceStatus Status { get; init; }
    [MaxLength(120)] public string? PaymentMethod { get; init; }
    [MaxLength(120)] public string? PaymentReference { get; init; }
}

/// <summary>Aggregate counts + totals shown above the invoices list so the admin gets a
/// at-a-glance feel for outstanding balances.</summary>
public record InvoiceSummaryDto(
    int TotalCount,
    int NewCount,
    int SentCount,
    int PaidCount,
    int ClosedCount,
    decimal OutstandingAmount,
    decimal PaidAmount);
