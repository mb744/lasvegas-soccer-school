using System.ComponentModel.DataAnnotations;

namespace SoccerSchool.Api.Domain;

/// <summary>
/// A coach's HR-style profile — full name, contact, mailing address, monthly payment, and the
/// list of coaching certifications they've earned. Distinct from <see cref="TeamCoach"/>, which
/// is just a per-team contact-card row used for messaging routing. A coach might exist in this
/// roster without being assigned to a specific team yet, and vice-versa.
/// </summary>
public class Coach
{
    public int Id { get; set; }

    [Required, MaxLength(80)]
    public string FirstName { get; set; } = string.Empty;

    [Required, MaxLength(80)]
    public string LastName { get; set; } = string.Empty;

    /// <summary>E.164-normalized on write, mirroring parent cell phones.</summary>
    [MaxLength(32)]
    public string? CellPhone { get; set; }

    public bool HasWhatsApp { get; set; }

    [MaxLength(256)]
    public string? Email { get; set; }

    // --- Mailing address (all optional; nothing is enforced beyond max length) ---

    [MaxLength(256)]
    public string? AddressLine1 { get; set; }

    [MaxLength(256)]
    public string? AddressLine2 { get; set; }

    [MaxLength(120)]
    public string? City { get; set; }

    [MaxLength(64)]
    public string? State { get; set; }

    [MaxLength(20)]
    public string? PostalCode { get; set; }

    /// <summary>Recurring monthly stipend / payment in USD. Null when the coach is unpaid or
    /// hasn't been signed up for a stipend yet.</summary>
    public decimal? MonthlyPayment { get; set; }

    /// <summary>Free-form notes the admin keeps on the coach (availability quirks, languages,
    /// preferred age group, etc.). Not surfaced to anyone but admins.</summary>
    [MaxLength(2000)]
    public string? Notes { get; set; }

    public Language Language { get; set; } = Language.English;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public List<CoachCertification> Certifications { get; set; } = new();
}
