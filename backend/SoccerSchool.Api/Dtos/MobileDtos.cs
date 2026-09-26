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

/// <summary>Request body for the Google / Facebook mobile OAuth exchange endpoints. Carries the
/// third-party token the app obtained via expo-auth-session (Google id_token, Facebook user
/// access token). Server verifies the token with the provider before minting LVSS tokens.</summary>
public record MobileExternalTokenRequest
{
    [Required]
    public string Token { get; init; } = string.Empty;
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
    /// <summary>True when this login's email appears on a TeamCoach card. Same coach-detection
    /// rule the web uses (see CoachScopeService). Coach-only actions on the mobile app gate on
    /// IsAdmin || IsCoach.</summary>
    bool IsCoach,
    IReadOnlyList<int> CoachTeamIds,
    IReadOnlyList<MobilePlayerDto> Players,
    /// <summary>False until the login proves it owns its email (confirmation link, a password
    /// reset by email, or Google/Facebook sign-in). Email-matched coach/family links wait on it.</summary>
    bool EmailConfirmed,
    /// <summary>Effective permission keys (see Auth/Permissions.cs) for showing/hiding UI. The
    /// server enforces them independently.</summary>
    IReadOnlyList<string> Permissions);

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
    string? VenueAddress,
    string? OpponentName,
    bool? IsHome,
    bool IsCancelled,
    string? UniformName,
    string? Notes,
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
    DateTime SentAt,
    MobileMediaDto? Media = null);

public record MobileSendMessageRequest
{
    /// <summary>Optional when <see cref="MediaId"/> is set (a photo/video with no caption).</summary>
    [MaxLength(4000)]
    public string? Body { get; init; }

    /// <summary>A Ready <c>MediaAsset</c> the caller uploaded, attached to this message.</summary>
    public int? MediaId { get; init; }
}

// ---- Media (photo/video uploads) ----

/// <summary>Url is a short-lived read SAS; clients should re-fetch rather than persist it.</summary>
public record MobileMediaDto(int MediaId, MediaKind Kind, string ContentType, string Url);

public record MobileCreateUploadRequest
{
    public MediaKind Kind { get; init; }

    [Required, MaxLength(100)]
    public string ContentType { get; init; } = string.Empty;

    public long SizeBytes { get; init; }
}

/// <summary>The device PUTs the file to <see cref="UploadUrl"/> with headers
/// <c>x-ms-blob-type: BlockBlob</c> and <c>Content-Type</c>, then calls /complete.</summary>
public record MobileCreateUploadResponse(int MediaId, string UploadUrl);

public record MobileEventMediaDto(
    int Id,
    int EventId,
    MobileMediaDto Media,
    string UploadedByUserId,
    string UploaderName,
    string? Caption,
    DateTime CreatedAt,
    bool CanDelete);

public record MobileAddEventMediaRequest
{
    public int MediaId { get; init; }

    [MaxLength(500)]
    public string? Caption { get; init; }
}

public record MobileReportMessageRequest
{
    [MaxLength(1000)]
    public string? Reason { get; init; }
}

public record MobileBlockedUserDto(string UserId, DateTime BlockedAt);

// ---- Invoices (parent-facing, read-only) ----

public record MobileInvoiceSummaryDto(
    int Id,
    string Description,
    decimal Amount,
    string Currency,
    DateOnly? DueDate,
    InvoiceStatus Status,
    DateTime IssuedAt,
    DateTime? PaidAt,
    string? PlayerName,
    string? ChargeTypeName);

public record MobileInvoiceDetailDto(
    int Id,
    string Description,
    decimal Amount,
    string Currency,
    DateOnly? DueDate,
    InvoiceStatus Status,
    DateTime IssuedAt,
    DateTime? SentAt,
    DateTime? PaidAt,
    string? PaymentMethod,
    string? PaymentReference,
    string? PlayerName,
    string? ChargeTypeName);

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
    /// <summary>True when this member's login/email appears on a TeamCoach card (same rule
    /// CoachScopeService uses). Lets the mobile chat-group admin surface tag coaches distinctly
    /// from parents and site admins.</summary>
    bool IsCoach,
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

/// <summary>Result row for the admin chat-group parent picker. Distinct from
/// <c>InboxParentDto</c> because chat is in-app: we don't require a phone number and we don't
/// exclude families flagged NoCommunications (that opt-out is for SMS/WhatsApp bulk sends only).</summary>
public record ChatParentSearchDto(
    int ParentAccountId,
    string Name,
    string? Email,
    string? Phone);
