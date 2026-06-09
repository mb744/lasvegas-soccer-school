using System.ComponentModel.DataAnnotations;
using SoccerSchool.Api.Domain;

namespace SoccerSchool.Api.Dtos;

public record UniformDto(
    int Id,
    string Name,
    string? ShirtColor,
    string? ShortsColor,
    string? SockColor,
    UniformDesignation Designation,
    DateTime CreatedAt,
    DateTime UpdatedAt);

public record SaveUniformRequest
{
    [Required, MaxLength(128)]
    public string Name { get; init; } = string.Empty;

    [MaxLength(64)]
    public string? ShirtColor { get; init; }

    [MaxLength(64)]
    public string? ShortsColor { get; init; }

    [MaxLength(64)]
    public string? SockColor { get; init; }

    /// <summary>None / Home / Away / Practice. Setting a non-None value steals it from any other
    /// uniform that currently holds it (one-per-designation).</summary>
    public UniformDesignation Designation { get; init; } = UniformDesignation.None;
}
