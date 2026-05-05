using System.ComponentModel.DataAnnotations;

namespace SoccerSchool.Api.Dtos;

public record PlayerSummary(
    int Id,
    string FirstName,
    string LastName,
    DateOnly DateOfBirth
);

public record SavePlayerRequest
{
    [Required, MaxLength(80)] public string FirstName { get; init; } = string.Empty;
    [Required, MaxLength(80)] public string LastName { get; init; } = string.Empty;
    [Required] public DateOnly DateOfBirth { get; init; }
}
