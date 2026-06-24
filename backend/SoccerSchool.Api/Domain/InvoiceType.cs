namespace SoccerSchool.Api.Domain;

/// <summary>
/// Categorizes an <see cref="Invoice"/> for reporting + admin filtering. Drives the badge
/// shown in the invoices table; doesn't change billing behavior (everything is a one-shot
/// invoice — Subscription just labels charges that repeat each cycle).
/// </summary>
public enum InvoiceType
{
    /// <summary>One-off charge (tournament fee, uniform fee, etc.).</summary>
    OneTime = 0,
    /// <summary>Recurring monthly subscription line (training, monthly dues).</summary>
    Subscription = 1,
}
