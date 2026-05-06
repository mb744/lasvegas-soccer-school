namespace SoccerSchool.Api.Dtos;

public record UserSummary(
    string Id,
    string Email,
    string FirstName,
    string LastName,
    string? Phone,
    bool IsAdmin,
    bool IsBanned,
    DateTime? CreatedAt,
    DateTime? LastLoginAt,
    int RegistrationCount
);
