using System.ComponentModel.DataAnnotations;
using SoccerSchool.Api.Domain;

namespace SoccerSchool.Api.Dtos;

// ============================================================================================
// Daily Training app (kids). Shapes mirror LVSSDailyTraining/src/api/types.ts — change both.
// ============================================================================================

// ---- Player auth ----

public record PlayerLoginRequest
{
    [Required, MaxLength(64)]
    public string Username { get; init; } = string.Empty;

    [Required, MaxLength(128)]
    public string Password { get; init; } = string.Empty;
}

public record PlayerForgotPasswordRequest
{
    [Required, MaxLength(64)]
    public string Username { get; init; } = string.Empty;
}

public record PlayerMeDto(int Id, string FirstName, string LastName, string Username, string? TeamName);

public record PlayerTokenResponse(
    string AccessToken,
    DateTime AccessTokenExpiresAt,
    string RefreshToken,
    DateTime RefreshTokenExpiresAt,
    PlayerMeDto Player);

// ---- Training ----

public record LocalizedTextDto(string En, string Es);

public record TrainingActivityDto(
    int Id,
    string Category,
    LocalizedTextDto Title,
    LocalizedTextDto Description,
    IReadOnlyList<LocalizedTextDto> Steps,
    int DurationMinutes,
    int? Reps,
    string? VideoUrl,
    bool Completed,
    DateTime? CompletedAt);

public record TodayPlanDto(DateOnly Date, IReadOnlyList<TrainingActivityDto> Activities);

public record DayProgressDto(DateOnly Date, int Completed, int Total);

public record TrainingProgressDto(int CurrentStreak, int BestStreak, int TotalCompleted, IReadOnlyList<DayProgressDto> Last7Days);

// ---- Parent: manage a child's training login ----

public record PlayerTrainingLoginDto(bool HasLogin, string? Username, DateTime? LastLoginAt);

public record SavePlayerTrainingLoginRequest
{
    [Required, MaxLength(64)]
    public string Username { get; init; } = string.Empty;

    /// <summary>Required when creating the login; optional on update (omit to keep the current one).</summary>
    [MaxLength(128)]
    public string? Password { get; init; }
}

// ---- Parent: password reset via emailed link (public, token-authorized) ----

public record PlayerPasswordResetInfoDto(string PlayerFirstName, string Username);

public record PlayerPasswordResetRequest
{
    [Required, MaxLength(128)]
    public string Token { get; init; } = string.Empty;

    [Required, MaxLength(128)]
    public string NewPassword { get; init; } = string.Empty;
}

// ---- Admin: drills + assignments ----

public record AdminDrillDto(
    int Id,
    string Category,
    string TitleEn,
    string TitleEs,
    string DescriptionEn,
    string DescriptionEs,
    string StepsEn,
    string StepsEs,
    int DurationMinutes,
    int? Reps,
    string? VideoUrl,
    bool IsActive,
    int AssignmentCount,
    DateTime UpdatedAt);

public record SaveDrillRequest
{
    [Required, MaxLength(32)]
    public string Category { get; init; } = string.Empty;

    [Required, MaxLength(120)]
    public string TitleEn { get; init; } = string.Empty;

    [Required, MaxLength(120)]
    public string TitleEs { get; init; } = string.Empty;

    [MaxLength(1000)]
    public string? DescriptionEn { get; init; }

    [MaxLength(1000)]
    public string? DescriptionEs { get; init; }

    [MaxLength(4000)]
    public string? StepsEn { get; init; }

    [MaxLength(4000)]
    public string? StepsEs { get; init; }

    [Range(1, 120)]
    public int DurationMinutes { get; init; }

    [Range(1, 1000)]
    public int? Reps { get; init; }

    [MaxLength(512)]
    public string? VideoUrl { get; init; }

    public bool IsActive { get; init; } = true;
}

public record AdminDrillAssignmentDto(
    int Id,
    int DrillId,
    string DrillTitle,
    string TargetType,
    int TargetId,
    string TargetName,
    DateOnly StartDate,
    DateOnly? EndDate,
    DateTime CreatedAt);

public record CreateDrillAssignmentRequest
{
    public int DrillId { get; init; }

    /// <summary>"player", "team" or "age-group".</summary>
    [Required, MaxLength(16)]
    public string TargetType { get; init; } = string.Empty;

    public int TargetId { get; init; }

    public DateOnly StartDate { get; init; }

    public DateOnly? EndDate { get; init; }
}

public record DrillTargetOptionDto(int Id, string Name);

public record DrillTargetPlayerDto(int Id, string Name, string TeamName);

/// <summary>Assignment-picker options, shaped by who's asking. <see cref="IsAdmin"/> tells the page
/// whether to show authoring controls; coaches get their own teams' players inline.</summary>
public record DrillTargetOptionsDto(
    bool IsAdmin,
    IReadOnlyList<DrillTargetOptionDto> Teams,
    IReadOnlyList<DrillTargetOptionDto> AgeGroups,
    IReadOnlyList<DrillTargetPlayerDto> Players);

/// <summary>Wire names for <see cref="DrillCategory"/> and <see cref="DrillTargetType"/>. Kebab-case
/// strings (not enum ints) so the apps' TypeScript unions read naturally.</summary>
public static class TrainingWire
{
    private static readonly Dictionary<DrillCategory, string> CategoryNames = new()
    {
        [DrillCategory.Fitness] = "fitness",
        [DrillCategory.BallMastery] = "ball-mastery",
        [DrillCategory.Dribbling] = "dribbling",
        [DrillCategory.Passing] = "passing",
        [DrillCategory.Shooting] = "shooting",
        [DrillCategory.Stretching] = "stretching",
    };

    private static readonly Dictionary<DrillTargetType, string> TargetNames = new()
    {
        [DrillTargetType.Player] = "player",
        [DrillTargetType.Team] = "team",
        [DrillTargetType.AgeClassification] = "age-group",
    };

    public static string ToWire(this DrillCategory c) => CategoryNames[c];
    public static string ToWire(this DrillTargetType t) => TargetNames[t];

    public static bool TryParseCategory(string? value, out DrillCategory category)
    {
        foreach (var (k, v) in CategoryNames)
        {
            if (string.Equals(v, value, StringComparison.OrdinalIgnoreCase)) { category = k; return true; }
        }
        category = default;
        return false;
    }

    public static bool TryParseTargetType(string? value, out DrillTargetType type)
    {
        foreach (var (k, v) in TargetNames)
        {
            if (string.Equals(v, value, StringComparison.OrdinalIgnoreCase)) { type = k; return true; }
        }
        type = default;
        return false;
    }

    /// <summary>Splits the one-step-per-line text columns and pairs English/Spanish by position.
    /// A missing translation falls back to the other language rather than showing a blank step.</summary>
    public static IReadOnlyList<LocalizedTextDto> Steps(string stepsEn, string stepsEs)
    {
        static string[] Lines(string s) =>
            s.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var en = Lines(stepsEn);
        var es = Lines(stepsEs);
        var count = Math.Max(en.Length, es.Length);
        var steps = new List<LocalizedTextDto>(count);
        for (var i = 0; i < count; i++)
        {
            var e = i < en.Length ? en[i] : es[i];
            var s = i < es.Length ? es[i] : en[i];
            steps.Add(new LocalizedTextDto(e, s));
        }
        return steps;
    }

    public static TrainingActivityDto ToActivity(Drill d, DrillCompletion? completion) => new(
        d.Id,
        d.Category.ToWire(),
        new LocalizedTextDto(d.TitleEn, string.IsNullOrWhiteSpace(d.TitleEs) ? d.TitleEn : d.TitleEs),
        new LocalizedTextDto(d.DescriptionEn, string.IsNullOrWhiteSpace(d.DescriptionEs) ? d.DescriptionEn : d.DescriptionEs),
        Steps(d.StepsEn, d.StepsEs),
        d.DurationMinutes,
        d.Reps,
        d.VideoUrl,
        completion is not null,
        completion?.CompletedAt);
}
