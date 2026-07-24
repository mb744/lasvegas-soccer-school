using System.ComponentModel.DataAnnotations;
using SoccerSchool.Api.Domain;

namespace SoccerSchool.Api.Dtos;

public record VenueDto(
    int Id,
    string Name,
    string? Address,
    SurfaceType Surface,
    DateTime CreatedAt,
    DateTime UpdatedAt);

public record SaveVenueRequest
{
    [Required, MaxLength(128)]
    public string Name { get; init; } = string.Empty;

    [MaxLength(512)]
    public string? Address { get; init; }

    public SurfaceType Surface { get; init; } = SurfaceType.Unspecified;
}

/// <summary>One playing surface under a venue — "Field 1", "North Field", etc.</summary>
public record VenueFieldDto(
    int Id,
    int VenueId,
    string Name,
    string? Notes,
    DateTime CreatedAt,
    DateTime UpdatedAt);

public record SaveVenueFieldRequest
{
    [Required, MaxLength(80)]
    public string Name { get; init; } = string.Empty;

    [MaxLength(500)]
    public string? Notes { get; init; }
}
