using System.ComponentModel.DataAnnotations;
using SoccerSchool.Api.Domain;

namespace SoccerSchool.Api.Dtos;

/// <summary>List-row + detail view of a coach for the admin Coaches card. Certifications are
/// returned inline so the detail page renders without a second round-trip.</summary>
public record CoachDto(
    int Id,
    string FirstName,
    string LastName,
    string? CellPhone,
    bool HasWhatsApp,
    string? Email,
    string? AddressLine1,
    string? AddressLine2,
    string? City,
    string? State,
    string? PostalCode,
    decimal? MonthlyPayment,
    string? Notes,
    Language Language,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    IReadOnlyList<CoachCertificationDto> Certifications);

public record CoachCertificationDto(
    int Id,
    int CoachId,
    string Name,
    string? IssuingBody,
    DateOnly? IssuedOn,
    DateOnly? ExpiresOn,
    string? CertificateNumber,
    string? Notes,
    DateTime CreatedAt);

/// <summary>Lightweight summary for the coaches list — drops certs + notes so the list endpoint
/// doesn't drag every cert through the wire when the admin's just scanning the directory.</summary>
public record CoachSummary(
    int Id,
    string FirstName,
    string LastName,
    string? CellPhone,
    string? Email,
    decimal? MonthlyPayment,
    int CertificationCount,
    DateTime UpdatedAt);

public record SaveCoachRecordRequest
{
    [Required, MaxLength(80)] public string FirstName { get; init; } = string.Empty;
    [Required, MaxLength(80)] public string LastName { get; init; } = string.Empty;
    [MaxLength(32)] public string? CellPhone { get; init; }
    public bool HasWhatsApp { get; init; }
    [MaxLength(256)] public string? Email { get; init; }
    [MaxLength(256)] public string? AddressLine1 { get; init; }
    [MaxLength(256)] public string? AddressLine2 { get; init; }
    [MaxLength(120)] public string? City { get; init; }
    [MaxLength(64)] public string? State { get; init; }
    [MaxLength(20)] public string? PostalCode { get; init; }
    public decimal? MonthlyPayment { get; init; }
    [MaxLength(2000)] public string? Notes { get; init; }
    public Language Language { get; init; } = Language.English;
}

public record SaveCoachCertificationRequest
{
    [Required, MaxLength(160)] public string Name { get; init; } = string.Empty;
    [MaxLength(120)] public string? IssuingBody { get; init; }
    public DateOnly? IssuedOn { get; init; }
    public DateOnly? ExpiresOn { get; init; }
    [MaxLength(64)] public string? CertificateNumber { get; init; }
    [MaxLength(1000)] public string? Notes { get; init; }
}
