using System.ComponentModel.DataAnnotations;

namespace SoccerSchool.Api.Domain;

/// <summary>
/// One coaching credential earned by a <see cref="Coach"/> — e.g. "USSF Grassroots 4v4",
/// "United Soccer Coaches Diploma", "Concussion Awareness — CDC". A coach can have any number
/// of these and they're displayed as a list under the coach's profile on the admin Coaches card.
/// </summary>
public class CoachCertification
{
    public int Id { get; set; }

    public int CoachId { get; set; }
    public Coach? Coach { get; set; }

    [Required, MaxLength(160)]
    public string Name { get; set; } = string.Empty;

    /// <summary>Who issued the certification (US Soccer, United Soccer Coaches, CDC, etc.).
    /// Optional — left blank when the name itself already implies the issuer.</summary>
    [MaxLength(120)]
    public string? IssuingBody { get; set; }

    public DateOnly? IssuedOn { get; set; }
    public DateOnly? ExpiresOn { get; set; }

    [MaxLength(64)]
    public string? CertificateNumber { get; set; }

    [MaxLength(1000)]
    public string? Notes { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
