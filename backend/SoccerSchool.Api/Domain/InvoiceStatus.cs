namespace SoccerSchool.Api.Domain;

/// <summary>
/// Lifecycle state for an <see cref="Invoice"/>. Linear-ish: New → Sent → Paid → Closed.
/// "Closed" is a terminal state covering both write-offs and voided invoices the admin
/// doesn't expect to collect on (the existing PaidAt distinguishes which one).
/// </summary>
public enum InvoiceStatus
{
    /// <summary>Just created; admin hasn't sent the parent a notification yet.</summary>
    New = 0,
    /// <summary>Parent has been notified (manual mark or via an outbound broadcast).</summary>
    Sent = 1,
    /// <summary>Marked paid by the admin. <see cref="Invoice.PaidAt"/> records when.</summary>
    Paid = 2,
    /// <summary>Closed/voided — won't be collected. Use for refunds or write-offs.</summary>
    Closed = 3,
}
