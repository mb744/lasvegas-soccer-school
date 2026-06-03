using System.ComponentModel.DataAnnotations;
using SoccerSchool.Api.Domain;

namespace SoccerSchool.Api.Dtos;

public record SaveTeamRequest
{
    [Required, MaxLength(128)]
    public string Name { get; init; } = string.Empty;

    /// <summary>Either set EventId + TeamId directly, or paste the full schedule URL into
    /// <see cref="ScheduleUrl"/> and the server will parse the IDs out of it.</summary>
    public int? GotSportEventId { get; init; }
    public int? GotSportTeamId { get; init; }

    /// <summary>Convenience: paste the public schedule URL
    /// (https://system.gotsport.com/org_event/events/{eventId}/schedules?team={teamId})
    /// and the server extracts the IDs.</summary>
    [MaxLength(1024)]
    public string? ScheduleUrl { get; init; }

    /// <summary>Optional curated message group to default-target when picking a game from this team.</summary>
    public int? MessageGroupId { get; init; }
}

public record TeamSummary(
    int Id,
    string Name,
    int GotSportEventId,
    int GotSportTeamId,
    int? MessageGroupId,
    string? MessageGroupName,
    DateTime? LastSyncedAt,
    string? LastSyncMessage,
    int UpcomingGameCount,
    DateTime CreatedAt);

public record TeamDetail(
    int Id,
    string Name,
    int GotSportEventId,
    int GotSportTeamId,
    int? MessageGroupId,
    string? MessageGroupName,
    DateTime? LastSyncedAt,
    string? LastSyncMessage,
    DateTime CreatedAt,
    IReadOnlyList<ScheduledGameDto> UpcomingGames);

public record ScheduledGameDto(
    int Id,
    int TeamId,
    string TeamName,
    int? MessageGroupId,
    string? MessageGroupName,
    ScheduledEventKind Kind,
    DateTime StartsAt,
    DateTime? EndsAt,
    string? Summary,
    string? Location,
    string? Description,
    string? OpponentName,
    bool? IsHome,
    Guid? SeriesId,
    bool IsCancelled,
    DateTime? CancelledAt,
    int? TournamentId,
    string? TournamentName);

public record SavePracticeRequest
{
    public DateTime StartsAt { get; init; }

    public DateTime? EndsAt { get; init; }

    [MaxLength(512)]
    public string? Location { get; init; }

    [MaxLength(512)]
    public string? Summary { get; init; }
}

/// <summary>Admin-entered game (manual; not scraped from GotSport). Lives in the same
/// ScheduledGames table with Kind=Game and a synthesized ExternalUid (`manual-game-{guid}`)
/// so the unique (team, uid) index doesn't collide with scraped games.</summary>
public record SaveGameRequest
{
    public DateTime StartsAt { get; init; }

    public DateTime? EndsAt { get; init; }

    [MaxLength(256)]
    public string? OpponentName { get; init; }

    /// <summary>true = we're home, false = away, null = unknown.</summary>
    public bool? IsHome { get; init; }

    [MaxLength(512)]
    public string? Location { get; init; }

    [MaxLength(512)]
    public string? Summary { get; init; }

    /// <summary>Optional tournament this game belongs to (set when added from the Tournaments tab).</summary>
    public int? TournamentId { get; init; }
}

/// <summary>Create a recurring practice series. Each combination of (day-of-week × occurrence date
/// in the [StartDate, EndDate] range) materializes as its own ScheduledGame row sharing a SeriesId.</summary>
public record SavePracticeSeriesRequest
{
    /// <summary>First date the series may occur on (inclusive, local date).</summary>
    public DateTime StartDate { get; init; }

    /// <summary>Last date the series may occur on (inclusive, local date).</summary>
    public DateTime EndDate { get; init; }

    /// <summary>Local time-of-day each occurrence starts at, in HH:mm 24-hour format
    /// (e.g. "17:00" for 5pm). Combined with each matching date to form StartsAt.</summary>
    [Required]
    public string StartTime { get; init; } = "17:00";

    /// <summary>Optional local end time-of-day, same HH:mm format.</summary>
    public string? EndTime { get; init; }

    /// <summary>Which days of the week the practice happens, 0 = Sunday … 6 = Saturday
    /// (matches <see cref="DayOfWeek"/>). At least one required.</summary>
    public int[] DaysOfWeek { get; init; } = Array.Empty<int>();

    [MaxLength(512)]
    public string? Location { get; init; }

    [MaxLength(512)]
    public string? Summary { get; init; }
}

public record PracticeSeriesCreatedDto(Guid SeriesId, int Count, IReadOnlyList<ScheduledGameDto> Occurrences);

/// <summary>One previously-notified parent for the cancel-notification flow. Sourced from the
/// BroadcastRecipient rows of any past broadcasts that referenced the cancelled event.</summary>
public record EventRecipientDto(string Phone, string? Name, Language Language);

public record ScheduleSyncResultDto(bool Success, int Added, int Updated, string Message);

// --- Event attendance (per rostered player confirmation) ---

/// <summary>One rostered player's confirmation status for an event. <see cref="Source"/> is 1 when
/// an admin set it manually, 0 when it came from a parent's reply. <see cref="UpdatedAt"/> is null
/// when there's no stored row yet (player defaults to Pending).</summary>
public record EventAttendanceDto(
    int PlayerId,
    string FirstName,
    string LastName,
    string? ParentName,
    string? ParentPhone,
    AttendanceStatus Status,
    AttendanceSource Source,
    DateTime? UpdatedAt);

public record EventAttendanceListDto(
    int EventId,
    int Confirmed,
    int Declined,
    int Maybe,
    int Pending,
    IReadOnlyList<EventAttendanceDto> Items);

/// <summary>Per-event confirmation counts for a team, used to badge each schedule row.</summary>
public record EventAttendanceSummaryDto(
    int EventId,
    int Confirmed,
    int Declined,
    int Maybe,
    int Pending);

public record SetAttendanceRequest
{
    public AttendanceStatus Status { get; init; }
}

// --- Tournaments (admin-owned event with optional GotSport import) ---

public record TournamentSummary(
    int Id,
    string Name,
    TournamentKind Kind,
    DateOnly? StartDate,
    DateOnly? EndDate,
    decimal? TotalCost,
    decimal? CostPerPlayer,
    int? TeamId,
    string? TeamName,
    int GotSportEventId,
    int GotSportTeamId,
    DateTime? LastSyncedAt,
    string? LastSyncMessage,
    int GameCount,
    int UpcomingGameCount,
    int RosterCount,
    DateTime CreatedAt,
    /// <summary>All teams participating in this tournament via the TournamentTeams join. Each
    /// carries its own GotSport sync state. Existing legacy tournaments show up here too via the
    /// auto-backfill migration.</summary>
    List<TournamentTeamDto> Teams);

/// <summary>Create/update fields for a tournament. Only Name is required; dates + costs are
/// nullable so admins can stub a tournament and fill in details later. GotSport fields stay
/// for legacy imports; new tournaments leave them at 0.</summary>
public record SaveTournamentRequest
{
    [Required, MaxLength(128)]
    public string Name { get; init; } = string.Empty;

    /// <summary>Tournament or League. Defaults to Tournament for backward compat.</summary>
    public TournamentKind Kind { get; init; } = TournamentKind.Tournament;

    public DateOnly? StartDate { get; init; }
    public DateOnly? EndDate { get; init; }
    public decimal? TotalCost { get; init; }
    public decimal? CostPerPlayer { get; init; }

    /// <summary>Optional explicit team id. Normally the admin creates the team via
    /// <c>POST /tournaments/{id}/team</c> after creating the tournament, so this stays null
    /// at create time.</summary>
    public int? TeamId { get; init; }

    public int? GotSportEventId { get; init; }
    public int? GotSportTeamId { get; init; }

    [MaxLength(1024)]
    public string? ScheduleUrl { get; init; }
}

/// <summary>Legacy: inlines the tournament's single dedicated team. New code path uses
/// <see cref="AddTournamentTeamRequest"/> against the TournamentTeams join.</summary>
public record CreateTournamentTeamRequest
{
    [Required, MaxLength(128)]
    public string Name { get; init; } = string.Empty;
}

/// <summary>One team participating in a tournament. Carries the per-participation GotSport
/// sync state — distinct from the team's own season-wide GotSport ID pair.</summary>
public record TournamentTeamDto(
    int Id,
    int TournamentId,
    int TeamId,
    string TeamName,
    int GotSportEventId,
    int GotSportTeamId,
    int TeamSnapEventId,
    int TeamSnapDivisionId,
    int TeamSnapParticipantId,
    DateTime? LastSyncedAt,
    string? LastSyncMessage,
    int RosterCount,
    int GameCount,
    DateTime CreatedAt);

/// <summary>Admin adds a team to a tournament. Either <see cref="ExistingTeamId"/> picks one of
/// the regular Teams admin rows, or <see cref="NewTeamName"/> creates a brand-new team. GotSport
/// or TeamSnap IDs are optional and can be set later via <see cref="UpdateTournamentTeamRequest"/>.</summary>
public record AddTournamentTeamRequest
{
    public int? ExistingTeamId { get; init; }

    [MaxLength(128)]
    public string? NewTeamName { get; init; }

    public int? GotSportEventId { get; init; }
    public int? GotSportTeamId { get; init; }
    public int? TeamSnapEventId { get; init; }
    public int? TeamSnapDivisionId { get; init; }
    public int? TeamSnapParticipantId { get; init; }

    [MaxLength(1024)]
    public string? ScheduleUrl { get; init; }
}

/// <summary>Body for the manual TeamSnap paste import. The admin copies the schedule rows
/// out of the TeamSnap UI ("Date / Time / Venue / Game / Team / Score / Score / Team")
/// and posts the raw text; the parser extracts each game and upserts the rows where the
/// chosen team name appears as home or away. <see cref="TeamNameOverride"/> lets the admin
/// search for a different label than the team's DB name (TeamSnap rosters often use a
/// longer variant — "Las Vegas Soccer School B17 Red" vs our "LVSS B17 Red").</summary>
public record ImportPastedScheduleRequest(string Text, string? TeamNameOverride = null);

/// <summary>Update the per-participation sync state for one TournamentTeam row. Set either the
/// GotSport pair OR the TeamSnap triple (event + division + participant); whichever has both
/// halves populated drives the sync.</summary>
public record UpdateTournamentTeamRequest
{
    public int? GotSportEventId { get; init; }
    public int? GotSportTeamId { get; init; }
    public int? TeamSnapEventId { get; init; }
    public int? TeamSnapDivisionId { get; init; }
    public int? TeamSnapParticipantId { get; init; }

    [MaxLength(1024)]
    public string? ScheduleUrl { get; init; }
}

public record TournamentAttendanceDto(
    int PlayerId,
    string FirstName,
    string LastName,
    string? ParentName,
    string? ParentPhone,
    AttendanceStatus Status,
    AttendanceSource Source,
    DateTime? UpdatedAt,
    bool Paid);

public record TournamentAttendanceListDto(
    int TournamentId,
    int Confirmed,
    int Declined,
    int Maybe,
    int Pending,
    int Paid,
    int Unpaid,
    List<TournamentAttendanceDto> Items);

/// <summary>Body for the per-player paid-flag toggle. Source is recorded so we know the
/// admin set it manually (the parent-reply path doesn't touch this flag).</summary>
public record SetTournamentPaidRequest(bool Paid);

/// <summary>Optional body for the tournament-confirmations send. <see cref="Overrides"/> carries
/// per-variable values edited inline in the preview modal — keyed by template variable position.
/// At fan-out time these overlay the computed values for every player, except for variables
/// mapped to a per-player property (PropertyKey starting with "player.") so the player name
/// still varies per recipient.</summary>
public record SendTournamentConfirmationsRequest(Dictionary<int, string>? Overrides);

public record SendTournamentConfirmationsResult(
    int Sent,
    int Skipped,
    int Total,
    string? Message,
    /// <summary>How many otherwise-matched players were excluded from this resend because
    /// their last failure was WhatsApp 131049 within the rate-limit backoff window. The admin
    /// is told the count so they know retries weren't silently dropped.</summary>
    int RateLimitedSkipped = 0);

/// <summary>Filters for the per-team "Re-send confirmations" flow. The admin opens a small
/// dialog and ticks which buckets to include — each filter ORs into the included set:
/// <list type="bullet">
///   <item>IncludeFailed: last-broadcast delivery status was Failed or Undelivered (every family
///   recipient failed).</item>
///   <item>IncludeUndelivered: never had a broadcast with PlayerId — e.g. a player added after
///   the original send. Also covers stuck Pending/Queued/Sent that never got a status callback.</item>
///   <item>IncludeNoResponse: TournamentAttendance.Status is still Pending (no Yes/No reply yet).</item>
/// </list>
/// At least one filter must be true; if none match any player, the response Sent=0.</summary>
public record ResendTournamentConfirmationsRequest(
    bool IncludeFailed,
    bool IncludeUndelivered,
    bool IncludeNoResponse);

/// <summary>Side-by-side EN/ES preview of the tournament confirmation message before the admin
/// confirms the fan-out. Built from one sample roster player; the actual send fills the player
/// name variable per recipient. Format strings (dates, cost) come from the same code path the
/// send uses, so the preview is byte-identical to what parents will see.
///
/// The frontend uses <see cref="TemplateId"/> + <see cref="Variables"/> to render editable
/// inputs in the preview modal — admin edits a value, calls /template-preview with the
/// updated values, and the EN/ES panes re-render live.</summary>
public record TournamentSendPreviewDto(
    string SamplePlayerName,
    string DatesValue,
    string CostValue,
    int RosterCount,
    int TemplateId,
    List<TournamentSendPreviewVariableDto> Variables,
    string EnglishTemplateName,
    string? EnglishRendered,
    string SpanishTemplateName,
    string? SpanishRendered);

public record TournamentSendPreviewVariableDto(
    int Position,
    string Label,
    string? PropertyKey,
    string Value);
