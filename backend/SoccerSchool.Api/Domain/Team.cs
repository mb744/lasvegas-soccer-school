using System.ComponentModel.DataAnnotations;

namespace SoccerSchool.Api.Domain;

/// <summary>
/// A team whose schedule comes from an external source — currently the public GotSport event
/// schedule page. GotSport has no public API and the iCal feature isn't exposed for every team,
/// so we scrape <c>https://system.gotsport.com/org_event/events/{EventId}/schedules?team={TeamId}</c>
/// which is publicly accessible. Optionally linked to a curated <see cref="MessageGroup"/> so
/// picking a game in the Compose flow can auto-target that team's parents.
/// </summary>
public class Team
{
    public int Id { get; set; }

    [Required, MaxLength(128)]
    public string Name { get; set; } = string.Empty;

    /// <summary>GotSport event/tournament ID (the number in <c>/org_event/events/{N}/schedules</c>).
    /// Required for scrape sync.</summary>
    public int GotSportEventId { get; set; }

    /// <summary>GotSport team ID — the <c>team=</c> query param value. Used both to fetch the
    /// per-team schedule view and to detect home/away by matching the (H)/(A)-suffixed link.</summary>
    public int GotSportTeamId { get; set; }

    /// <summary>Optional link to a curated message group of this team's parents. When set,
    /// picking a game from this team in Compose defaults the broadcast recipient to that group.</summary>
    public int? MessageGroupId { get; set; }
    public MessageGroup? MessageGroup { get; set; }

    public DateTime? LastSyncedAt { get; set; }

    [MaxLength(512)]
    public string? LastSyncMessage { get; set; }

    // --- Coach contact (optional) ---

    [MaxLength(160)]
    public string? CoachName { get; set; }

    [MaxLength(256)]
    public string? CoachEmail { get; set; }

    /// <summary>E.164-normalized on write, like parent cell numbers.</summary>
    [MaxLength(32)]
    public string? CoachPhone { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public List<ScheduledGame> Games { get; set; } = new();

    /// <summary>Players assigned to this team's roster. Drives team-building and the roster-based
    /// messaging audience.</summary>
    public List<TeamPlayer> Roster { get; set; } = new();
}
