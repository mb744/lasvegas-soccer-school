using System.ComponentModel.DataAnnotations;
using SoccerSchool.Api.Domain;

namespace SoccerSchool.Api.Dtos;

public record RegistrationPlayerInput
{
    /// <summary>If set, attach this existing roster Player. If null, create a new Player from the inline fields.</summary>
    public int? PlayerId { get; init; }

    [MaxLength(80)] public string? FirstName { get; init; }
    [MaxLength(80)] public string? LastName { get; init; }
    public DateOnly? DateOfBirth { get; init; }

    // Only name + DOB are required across every player-add surface (public submit, admin add,
    // /account add). Grade/uniform/shoe are kept as the convenience fields they always were but
    // no longer block submission when blank.
    [MaxLength(40)] public string SchoolGrade { get; init; } = string.Empty;
    [MaxLength(10)] public string UniformSize { get; init; } = string.Empty;
    [MaxLength(10)] public string ShoeSize { get; init; } = string.Empty;
    [MaxLength(120)] public string? HeardFrom { get; init; }

    [MaxLength(160)] public string? WaiverParticipantName { get; init; }
    [MaxLength(120)] public string? WaiverTeamName { get; init; }
    [MaxLength(160)] public string? WaiverParentGuardianName { get; init; }
    [MaxLength(32)] public string? WaiverPhone { get; init; }
    [MaxLength(256)] public string? WaiverEmail { get; init; }

    /// <summary>data:image/png;base64,... signature image, required.</summary>
    [Required]
    public string SignatureDataUrl { get; init; } = string.Empty;
}

/// <summary>An additional parent/guardian contact submitted with a registration or admin edit.
/// At least a name plus one of Email/CellPhone should be present; fully-blank rows are ignored.</summary>
public record ParentContactInput
{
    [MaxLength(80)] public string? FirstName { get; init; }
    [MaxLength(80)] public string? LastName { get; init; }
    [MaxLength(256)] public string? Email { get; init; }
    [MaxLength(32)] public string? CellPhone { get; init; }
    public bool HasWhatsApp { get; init; }
    /// <summary>When null, defaults to the registration's language on sync.</summary>
    public Language? Language { get; init; }
}

public record ParentContactDto(
    int Id,
    string FirstName,
    string LastName,
    string? Email,
    string? CellPhone,
    bool HasWhatsApp,
    Language Language);

public record SubmitRegistrationRequest
{
    [Required, MaxLength(80)] public string ParentFirstName { get; init; } = string.Empty;
    [Required, MaxLength(80)] public string ParentLastName { get; init; } = string.Empty;
    [MaxLength(200)] public string? AddressLine1 { get; init; }
    [MaxLength(200)] public string? AddressLine2 { get; init; }
    [MaxLength(80)] public string? City { get; init; }
    [MaxLength(40)] public string? State { get; init; }
    [MaxLength(20)] public string? PostalCode { get; init; }
    [Required, MaxLength(32)] public string CellPhone { get; init; } = string.Empty;
    [Required, EmailAddress, MaxLength(256)] public string Email { get; init; } = string.Empty;

    public Language Language { get; init; } = Language.English;

    /// <summary>Whether the parent confirmed WhatsApp is installed on the cell they provided. Required.</summary>
    public bool HasWhatsApp { get; init; }

    [Required] public bool WaiverConsent { get; init; }

    [Required, MinLength(1)]
    public List<RegistrationPlayerInput> Players { get; init; } = new();

    /// <summary>Additional parent/guardian contacts. Replace-all synced onto the family account.</summary>
    public List<ParentContactInput> AdditionalParents { get; init; } = new();
}

/// <summary>Admin-only edit. Updates contact info + flags on an existing registration. Does NOT
/// touch waiver consent, signatures, or the player list — those are immutable artifacts of the
/// original submission.</summary>
public record UpdateRegistrationRequest
{
    [Required, MaxLength(80)] public string ParentFirstName { get; init; } = string.Empty;
    [Required, MaxLength(80)] public string ParentLastName { get; init; } = string.Empty;
    [MaxLength(200)] public string? AddressLine1 { get; init; }
    [MaxLength(200)] public string? AddressLine2 { get; init; }
    [MaxLength(80)] public string? City { get; init; }
    [MaxLength(40)] public string? State { get; init; }
    [MaxLength(20)] public string? PostalCode { get; init; }
    [Required, MaxLength(32)] public string CellPhone { get; init; } = string.Empty;
    [Required, EmailAddress, MaxLength(256)] public string Email { get; init; } = string.Empty;
    public Language Language { get; init; } = Language.English;
    public bool HasWhatsApp { get; init; }

    /// <summary>Additional parent/guardian contacts. Replace-all synced onto the family account.</summary>
    public List<ParentContactInput> AdditionalParents { get; init; } = new();
}

public record RegistrationSummary(
    int Id,
    string Season,
    string ParentFirstName,
    string ParentLastName,
    string Email,
    string CellPhone,
    Language Language,
    bool HasWhatsApp,
    int PlayerCount,
    DateTime CreatedAt
);

public record RegistrationDetail(
    int Id,
    string Season,
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
    bool HasWhatsApp,
    bool WaiverConsent,
    DateTime? WaiverSignedAt,
    DateTime CreatedAt,
    List<RegistrationPlayerDetail> Players,
    List<ParentContactDto> AdditionalParents
);

public record RegistrationPlayerDetail(
    int Id,
    int PlayerId,
    string FirstName,
    string LastName,
    DateOnly DateOfBirth,
    string SchoolGrade,
    string UniformSize,
    string ShoeSize,
    string? HeardFrom,
    string? WaiverParticipantName,
    string? WaiverTeamName,
    string? WaiverParentGuardianName,
    string? WaiverPhone,
    string? WaiverEmail,
    bool HasSignature,
    DateTime? SignedAt,
    bool FreeTrialOver,
    int? AgeClassificationId,
    string? AgeClassificationName
);

public record UpdatePlayerTrialRequest
{
    public bool FreeTrialOver { get; init; }
}

/// <summary>Admin adds a player to an existing registration (creates the durable player too).
/// No signature — the row is enrolled but unsigned until a waiver is captured later. Only
/// name + DOB are required; grade/uniform/shoe can be filled in later as that info shows up.</summary>
public record AddRegistrationPlayerRequest
{
    [Required, MaxLength(80)] public string FirstName { get; init; } = string.Empty;
    [Required, MaxLength(80)] public string LastName { get; init; } = string.Empty;
    public DateOnly DateOfBirth { get; init; }
    [MaxLength(40)] public string SchoolGrade { get; init; } = string.Empty;
    [MaxLength(10)] public string UniformSize { get; init; } = string.Empty;
    [MaxLength(10)] public string ShoeSize { get; init; } = string.Empty;
    [MaxLength(120)] public string? HeardFrom { get; init; }
}

/// <summary>Admin edit of an enrolled player: per-season fields plus the durable name/DOB
/// (DOB change re-assigns the age bracket; name/DOB changes apply to the player everywhere).
/// Only name + DOB are required; the size/grade fields can be left blank.</summary>
public record UpdateRegistrationPlayerRequest
{
    [Required, MaxLength(80)] public string FirstName { get; init; } = string.Empty;
    [Required, MaxLength(80)] public string LastName { get; init; } = string.Empty;
    public DateOnly DateOfBirth { get; init; }
    [MaxLength(40)] public string SchoolGrade { get; init; } = string.Empty;
    [MaxLength(10)] public string UniformSize { get; init; } = string.Empty;
    [MaxLength(10)] public string ShoeSize { get; init; } = string.Empty;
}

/// <summary>Admin spins up an empty registration shell for an existing parent account so the
/// parent can log in and finish it (consent + sign waivers per player). Backend blocks duplicates
/// in the same season — admins should edit the existing one instead.</summary>
public record AdminCreateRegistrationRequest
{
    [Required] public int ParentAccountId { get; init; }
    /// <summary>Season label. When null/blank, the backend defaults to the configured active season.</summary>
    [MaxLength(40)] public string? Season { get; init; }
}

/// <summary>Parent captures the signature for a single enrolled player. Stamps SignedAt and, when
/// the registration hasn't been waiver-consented yet, marks the whole registration consented too.</summary>
public record SignPlayerWaiverRequest
{
    [Required] public string SignatureDataUrl { get; init; } = string.Empty;
}

// --- Age classifications (admin-managed DOB buckets) ---

public record AgeClassificationDto(
    int Id,
    string Name,
    string? Description,
    DateOnly DobStart,
    DateOnly DobEnd,
    DateTime CreatedAt,
    DateTime UpdatedAt);

public record SaveAgeClassificationRequest
{
    [Required, MaxLength(64)]
    public string Name { get; init; } = string.Empty;

    [MaxLength(256)]
    public string? Description { get; init; }

    public DateOnly DobStart { get; init; }
    public DateOnly DobEnd { get; init; }
}
