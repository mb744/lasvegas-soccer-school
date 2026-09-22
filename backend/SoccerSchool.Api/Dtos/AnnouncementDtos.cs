using System.ComponentModel.DataAnnotations;

namespace SoccerSchool.Api.Dtos;

public record AnnouncementDto(
    int Id,
    string Title,
    string Body,
    int? TeamId,
    string? TeamName,
    DateTime? EndsAt,
    bool IsActive,
    DateTime CreatedAt,
    DateTime UpdatedAt);

public record SaveAnnouncementRequest
{
    [Required, MaxLength(200)]
    public string Title { get; init; } = string.Empty;

    [Required, MaxLength(2000)]
    public string Body { get; init; } = string.Empty;

    /// <summary>Null = school-wide.</summary>
    public int? TeamId { get; init; }

    public DateTime? EndsAt { get; init; }

    public bool IsActive { get; init; } = true;
}

/// <summary>Parent-facing shape — no admin metadata, no CreatedByUserId.</summary>
public record MobileAnnouncementDto(
    int Id,
    string Title,
    string Body,
    int? TeamId,
    string? TeamName,
    DateTime CreatedAt);
