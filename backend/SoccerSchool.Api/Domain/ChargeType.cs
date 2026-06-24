using System.ComponentModel.DataAnnotations;

namespace SoccerSchool.Api.Domain;

/// <summary>
/// Admin-managed catalog of billable charges — "Tournament fee", "Monthly training", "Uniform
/// fee", etc. Each one carries a default amount and a recurrence cadence. When the admin
/// creates an <see cref="Invoice"/> they can pick a charge type to pre-fill the invoice's
/// description + amount; the invoice keeps a pointer back to the type for reporting.
/// </summary>
public class ChargeType
{
    public int Id { get; set; }

    [Required, MaxLength(128)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(1000)]
    public string? Description { get; set; }

    /// <summary>Default amount in USD. Same precision as Invoice.Amount.</summary>
    public decimal Amount { get; set; }

    public ChargeRecurrence Recurrence { get; set; } = ChargeRecurrence.OneTime;

    /// <summary>Soft-disable. Inactive types stay in the DB for historical invoices to
    /// reference but don't show in the create-invoice picker.</summary>
    public bool Active { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
