using System.ComponentModel.DataAnnotations;
using SoccerSchool.Api.Domain;

namespace SoccerSchool.Api.Dtos;

public record SignupRequest
{
    [Required, EmailAddress, MaxLength(256)]
    public string Email { get; init; } = string.Empty;

    [Required, MinLength(8), MaxLength(128)]
    public string Password { get; init; } = string.Empty;

    [Required, MaxLength(80)]
    public string FirstName { get; init; } = string.Empty;

    [Required, MaxLength(80)]
    public string LastName { get; init; } = string.Empty;

    [MaxLength(32)]
    public string? Phone { get; init; }

    public Language Language { get; init; } = Language.English;
}

public record LoginRequest
{
    [Required, EmailAddress, MaxLength(256)]
    public string Email { get; init; } = string.Empty;

    [Required, MaxLength(128)]
    public string Password { get; init; } = string.Empty;

    public bool RememberMe { get; init; } = true;
}

public record MeResponse(
    string UserId,
    string Email,
    string FirstName,
    string LastName,
    string? Phone,
    Language Language,
    bool IsAdmin
);
