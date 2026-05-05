using System.ComponentModel.DataAnnotations;
using SoccerSchool.Api.Domain;

namespace SoccerSchool.Api.Dtos;

public record PlayerDto
{
    [Required, MaxLength(80)] public string FirstName { get; init; } = string.Empty;
    [Required, MaxLength(80)] public string LastName { get; init; } = string.Empty;
    [Required] public DateOnly DateOfBirth { get; init; }
    [Required, MaxLength(40)] public string SchoolGrade { get; init; } = string.Empty;
    [Required, MaxLength(10)] public string ShirtSize { get; init; } = string.Empty;
    [Required, MaxLength(10)] public string ShortSize { get; init; } = string.Empty;
    [Required, MaxLength(10)] public string ShoeSize { get; init; } = string.Empty;
    [MaxLength(120)] public string? HeardFrom { get; init; }

    // Per-player waiver (editable, prepopulated client-side)
    [MaxLength(160)] public string? WaiverParticipantName { get; init; }
    [MaxLength(120)] public string? WaiverTeamName { get; init; }
    [MaxLength(160)] public string? WaiverParentGuardianName { get; init; }
    [MaxLength(32)] public string? WaiverPhone { get; init; }
    [MaxLength(256)] public string? WaiverEmail { get; init; }

    /// <summary>data:image/png;base64,... signature image, required.</summary>
    [Required]
    public string SignatureDataUrl { get; init; } = string.Empty;
}

public record SubmitRegistrationRequest
{
    [Required] public string Token { get; init; } = string.Empty;

    [Required, MaxLength(80)] public string ParentFirstName { get; init; } = string.Empty;
    [Required, MaxLength(80)] public string ParentLastName { get; init; } = string.Empty;
    [Required, MaxLength(200)] public string AddressLine1 { get; init; } = string.Empty;
    [MaxLength(200)] public string? AddressLine2 { get; init; }
    [Required, MaxLength(80)] public string City { get; init; } = string.Empty;
    [Required, MaxLength(40)] public string State { get; init; } = "NV";
    [Required, MaxLength(20)] public string PostalCode { get; init; } = string.Empty;
    [Required, MaxLength(32)] public string CellPhone { get; init; } = string.Empty;
    [Required, EmailAddress, MaxLength(256)] public string Email { get; init; } = string.Empty;

    public Language Language { get; init; } = Language.English;

    [Required] public bool WaiverConsent { get; init; }

    [Required, MinLength(1)]
    public List<PlayerDto> Players { get; init; } = new();
}

public record RegistrationSummary(
    int Id,
    string ParentFirstName,
    string ParentLastName,
    string Email,
    string CellPhone,
    Language Language,
    int PlayerCount,
    DateTime CreatedAt
);

public record RegistrationDetail(
    int Id,
    string ParentFirstName,
    string ParentLastName,
    string AddressLine1,
    string? AddressLine2,
    string City,
    string State,
    string PostalCode,
    string CellPhone,
    string Email,
    Language Language,
    bool WaiverConsent,
    DateTime? WaiverSignedAt,
    DateTime CreatedAt,
    List<PlayerDetail> Players
);

public record PlayerDetail(
    int Id,
    string FirstName,
    string LastName,
    DateOnly DateOfBirth,
    string SchoolGrade,
    string ShirtSize,
    string ShortSize,
    string ShoeSize,
    string? HeardFrom,
    string? WaiverParticipantName,
    string? WaiverTeamName,
    string? WaiverParentGuardianName,
    string? WaiverPhone,
    string? WaiverEmail,
    bool HasSignature,
    DateTime? SignedAt
);
