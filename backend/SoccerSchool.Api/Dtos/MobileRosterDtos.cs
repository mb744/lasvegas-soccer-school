using SoccerSchool.Api.Domain;

namespace SoccerSchool.Api.Dtos;

public record MobileRosterTeamDto(int Id, string Name, int PlayerCount);

/// <summary>JerseyNumbers: distinct numbers on the player's active (not returned) uniforms.</summary>
public record MobileRosterPlayerDto(int PlayerId, string FirstName, string LastName, IReadOnlyList<string> JerseyNumbers);

public record MobileRosterDto(int TeamId, string TeamName, IReadOnlyList<MobileRosterPlayerDto> Players);

/// <summary>One parent/guardian of a player. IsPrimary marks the family's account holder.</summary>
public record MobileRosterGuardianDto(string Name, bool IsPrimary, string? Phone, string? Email, Language Language);

public record MobileRosterPlayerDetailDto(
    int PlayerId,
    string FirstName,
    string LastName,
    int TeamId,
    string TeamName,
    IReadOnlyList<string> JerseyNumbers,
    IReadOnlyList<MobileRosterGuardianDto> Guardians);
