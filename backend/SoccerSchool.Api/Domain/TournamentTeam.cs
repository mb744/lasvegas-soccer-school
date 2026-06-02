using System.ComponentModel.DataAnnotations;

namespace SoccerSchool.Api.Domain;

/// <summary>
/// One team's participation in one tournament. A tournament can have several teams (U10 boys,
/// U12 girls, etc.); a team can also appear in several tournaments over a season. GotSport IDs
/// live here, not on <see cref="Tournament"/>, because GotSport issues a fresh event/team-id
/// pair per competition, so each participation needs its own.
/// </summary>
public class TournamentTeam
{
    public int Id { get; set; }

    public int TournamentId { get; set; }
    public Tournament? Tournament { get; set; }

    public int TeamId { get; set; }
    public Team? Team { get; set; }

    /// <summary>GotSport event ID for THIS team in THIS tournament. 0 = no GotSport sync wired
    /// up; manual-only.</summary>
    public int GotSportEventId { get; set; }

    /// <summary>GotSport team ID — the <c>team=</c> query param on the schedule URL. 0 = none.</summary>
    public int GotSportTeamId { get; set; }

    public DateTime? LastSyncedAt { get; set; }

    [MaxLength(512)]
    public string? LastSyncMessage { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
