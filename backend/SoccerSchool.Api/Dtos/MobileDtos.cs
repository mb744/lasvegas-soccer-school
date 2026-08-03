using System.ComponentModel.DataAnnotations;
using SoccerSchool.Api.Domain;

namespace SoccerSchool.Api.Dtos;

// ---- Auth ----

public record MobileLoginRequest
{
    [Required, EmailAddress, MaxLength(256)]
    public string Email { get; init; } = string.Empty;

    [Required, MaxLength(128)]
    public string Password { get; init; } = string.Empty;
}

public record MobileRefreshRequest
{
    [Required]
    public string RefreshToken { get; init; } = string.Empty;
}

public record MobileTokenResponse(
    string AccessToken,
    DateTime AccessTokenExpiresAt,
    string RefreshToken,
    DateTime RefreshTokenExpiresAt,
    MobileMeResponse User);

public record MobileMeResponse(
    string UserId,
    string Email,
    string FirstName,
    string LastName,
    string? Phone,
    Language Language,
    bool IsAdmin,
    IReadOnlyList<MobilePlayerDto> Players);

// ---- Players ----

public record MobilePlayerDto(
    int Id,
    string FirstName,
    string LastName,
    DateOnly DateOfBirth,
    IReadOnlyList<MobilePlayerTeamDto> Teams);

public record MobilePlayerTeamDto(int TeamId, string TeamName);

// ---- Schedule + attendance ----

public record MobileScheduleEventDto(
    int Id,
    int TeamId,
    string TeamName,
    ScheduledEventKind Kind,
    DateTime StartsAt,
    DateTime? EndsAt,
    DateTime? ArriveAt,
    string? Summary,
    string? Location,
    string? VenueName,
    string? OpponentName,
    bool? IsHome,
    bool IsCancelled,
    string? UniformName,
    ShoeType ShoeType,
    IReadOnlyList<MobileEventPlayerDto> Players);

/// <summary>One of the caller's own kids on this event's team, with their current confirmation.</summary>
public record MobileEventPlayerDto(
    int PlayerId,
    string FirstName,
    string LastName,
    AttendanceStatus Status);

public record MobileSetAttendanceRequest
{
    public int PlayerId { get; init; }
    public AttendanceStatus Status { get; init; }
}

// ---- Chat ----

public record MobileChatGroupDto(
    int Id,
    string Title,
    string? LastMessagePreview,
    string? LastMessageSender,
    DateTime? LastMessageAt,
    int UnreadCount);

public record MobileChatMessageDto(
    int Id,
    int GroupId,
    string SenderUserId,
    string SenderName,
    bool IsFromAdmin,
    string Body,
    DateTime SentAt);

public record MobileSendMessageRequest
{
    [Required, MaxLength(4000)]
    public string Body { get; init; } = string.Empty;
}

// ---- Push devices ----

public record RegisterDeviceRequest
{
    [Required, MaxLength(256)]
    public string ExpoPushToken { get; init; } = string.Empty;

    public DevicePlatform Platform { get; init; } = DevicePlatform.Unknown;
}

// ---- Admin chat-group management (web) ----

public record ChatGroupAdminDto(
    int Id,
    string Title,
    int? TeamId,
    string? TeamName,
    int MemberCount,
    int MessageCount,
    DateTime CreatedAt,
    IReadOnlyList<ChatGroupMemberDto> Members);

public record ChatGroupMemberDto(
    int Id,
    int? ParentAccountId,
    string DisplayName,
    ChatMemberRole Role,
    DateTime AddedAt);

public record SaveChatGroupRequest
{
    [Required, MaxLength(128)]
    public string Title { get; init; } = string.Empty;

    /// <summary>When set, the group is created seeded with every parent on that team's roster.</summary>
    public int? SeedFromTeamId { get; init; }
}

public record AddChatGroupMemberRequest
{
    [Required]
    public int ParentAccountId { get; init; }
}
