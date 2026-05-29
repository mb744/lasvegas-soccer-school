using System.ComponentModel.DataAnnotations;

namespace SoccerSchool.Api.Domain;

/// <summary>
/// One of our teams' entry in a GotSport competition. Owns the GotSport event/team IDs and the
/// schedule sync (scrapes that team's games from the public event page). Synced games — and any
/// games an admin adds manually under the tournament — belong to <see cref="TeamId"/> and surface
/// in the Games tab. A team can have several tournaments over a season.
/// </summary>
public class Tournament
{
    public int Id { get; set; }

    [Required, MaxLength(128)]
    public string Name { get; set; } = string.Empty;

    /// <summary>The participating team. Its roster drives attendance + messaging for these games.</summary>
    public int TeamId { get; set; }
    public Team? Team { get; set; }

    /// <summary>GotSport event ID (the number in <c>/org_event/events/{N}/schedules</c>).</summary>
    public int GotSportEventId { get; set; }

    /// <summary>GotSport team ID — the <c>team=</c> query param, used to fetch the per-team view and
    /// detect home/away.</summary>
    public int GotSportTeamId { get; set; }

    public DateTime? LastSyncedAt { get; set; }

    [MaxLength(512)]
    public string? LastSyncMessage { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public List<ScheduledGame> Games { get; set; } = new();
}
