using System.ComponentModel.DataAnnotations;
using SoccerSchool.Api.Domain;

namespace SoccerSchool.Api.Dtos;

public record ChargeTypeDto(
    int Id,
    string Name,
    string? Description,
    decimal Amount,
    ChargeRecurrence Recurrence,
    bool Active,
    DateTime CreatedAt,
    DateTime UpdatedAt);

public record SaveChargeTypeRequest
{
    [Required, MaxLength(128)] public string Name { get; init; } = string.Empty;
    [MaxLength(1000)] public string? Description { get; init; }
    [Required, Range(0.01, 1_000_000)] public decimal Amount { get; init; }
    public ChargeRecurrence Recurrence { get; init; } = ChargeRecurrence.OneTime;
    public bool Active { get; init; } = true;
}
