using System.ComponentModel.DataAnnotations;

namespace SoccerSchool.Api.Domain;

/// <summary>
/// One billable charge owed by a <see cref="ParentAccount"/> — a tournament fee, a uniform
/// charge, a monthly subscription line, etc. Carries its own status state machine
/// (New → Sent → Paid → Closed) tracked by the admin Invoices card.
///
/// Doesn't itself send the parent any notification — admin moves to Sent manually after
/// the per-tournament fee broadcast or a one-off message goes out. Future enhancement:
/// auto-mark Sent when a Broadcast tagged with this InvoiceId is created.
/// </summary>
public class Invoice
{
    public int Id { get; set; }

    public int ParentAccountId { get; set; }
    public ParentAccount? ParentAccount { get; set; }

    /// <summary>What the charge is for — admin types this in. Surfaced in the invoices list
    /// and in any parent-facing message that references the invoice.</summary>
    [Required, MaxLength(256)]
    public string Description { get; set; } = string.Empty;

    /// <summary>Amount due in USD. Precision configured in AppDbContext so EF doesn't
    /// truncate cents.</summary>
    public decimal Amount { get; set; }

    /// <summary>3-letter currency code. Defaults to USD; future-proofs the table without
    /// constraining today's flow.</summary>
    [MaxLength(3)]
    public string Currency { get; set; } = "USD";

    public InvoiceType Type { get; set; } = InvoiceType.OneTime;

    public InvoiceStatus Status { get; set; } = InvoiceStatus.New;

    /// <summary>When the admin issued the invoice. Defaults to creation time and isn't
    /// editable post-create — historical accuracy matters here.</summary>
    public DateTime IssuedAt { get; set; } = DateTime.UtcNow;

    /// <summary>When the parent owes payment by. Optional; when null the invoice is
    /// open-ended (typical for subscription line items waiting on the monthly cycle).</summary>
    public DateOnly? DueDate { get; set; }

    /// <summary>Stamped when Status flips to Sent. Lets the admin see how long parents take
    /// to respond after the first notification.</summary>
    public DateTime? SentAt { get; set; }

    /// <summary>Stamped when Status flips to Paid. Reverting from Paid to another status
    /// clears this so audit logs stay clean.</summary>
    public DateTime? PaidAt { get; set; }

    /// <summary>How the parent paid — admin-entered free text ("Zelle", "Cash", "Check #1234").
    /// Set when marking Paid; stays null on New/Sent/Closed.</summary>
    [MaxLength(120)]
    public string? PaymentMethod { get; set; }

    /// <summary>Optional Zelle confirmation / check number / Stripe charge id, etc.</summary>
    [MaxLength(120)]
    public string? PaymentReference { get; set; }

    /// <summary>Admin-only notes. Never shown to parents.</summary>
    [MaxLength(2000)]
    public string? Notes { get; set; }

    /// <summary>AspNetUsers id of the admin who created the invoice. Null when the creating
    /// user was later deleted — the invoice itself stays.</summary>
    [MaxLength(450)]
    public string? CreatedByUserId { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
