using System.ComponentModel.DataAnnotations;
using SoccerSchool.Api.Domain;

namespace SoccerSchool.Api.Dtos;

public record CreateInvitationRequest
{
    [EmailAddress, MaxLength(256)]
    public string? Email { get; init; }

    [MaxLength(32)]
    public string? Phone { get; init; }

    public Language Language { get; init; } = Language.English;
}

public record InvitationResponse(
    int Id,
    string Token,
    string? Email,
    string? Phone,
    Language Language,
    InvitationStatus Status,
    string? StatusMessage,
    string Link,
    DateTime CreatedAt,
    DateTime? SentAt,
    DateTime? RegisteredAt
);

public record InvitationLookupResponse(
    string Token,
    Language Language,
    InvitationStatus Status,
    string? Email,
    string? Phone,
    bool AlreadyRegistered
);
