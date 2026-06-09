using System.ComponentModel.DataAnnotations;

namespace SoccerSchool.Api.Dtos;

public record MappedFieldDto(
    int Id,
    string Name,
    string Key,
    string Template,
    DateTime CreatedAt,
    DateTime UpdatedAt);

public record SaveMappedFieldRequest
{
    [Required, MaxLength(128)]
    public string Name { get; init; } = string.Empty;

    /// <summary>Composition with <c>{base.key}</c> placeholders, e.g.
    /// <c>{event.venue}, {event.address} — {event.field}</c>.</summary>
    [Required, MaxLength(1024)]
    public string Template { get; init; } = string.Empty;
}
