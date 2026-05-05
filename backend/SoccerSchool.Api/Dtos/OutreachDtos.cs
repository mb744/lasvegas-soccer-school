using System.ComponentModel.DataAnnotations;
using SoccerSchool.Api.Domain;

namespace SoccerSchool.Api.Dtos;

public record CreateOutreachRequest
{
    [EmailAddress, MaxLength(256)]
    public string? Email { get; init; }

    [MaxLength(32)]
    public string? Phone { get; init; }

    public Language Language { get; init; } = Language.English;
}

public record OutreachResponse(
    int Id,
    string? Email,
    string? Phone,
    Language Language,
    OutreachStatus Status,
    string? StatusMessage,
    string Link,
    DateTime CreatedAt,
    DateTime? SentAt,
    DateTime? AccountCreatedAt,
    DateTime? RegisteredAt,
    int? ParentAccountId
);
