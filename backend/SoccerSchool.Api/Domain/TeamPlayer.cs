namespace SoccerSchool.Api.Domain;

/// <summary>
/// Roster membership: a durable <see cref="Player"/> assigned to a <see cref="Team"/>. The roster
/// drives both team-building and the roster-based messaging audience. Membership is on the durable
/// player (not the per-season RegistrationPlayer) so it survives across seasons; season-specific
/// context (age bracket, grade) is looked up from the player's registrations for display.
/// </summary>
public class TeamPlayer
{
    public int Id { get; set; }

    public int TeamId { get; set; }
    public Team? Team { get; set; }

    public int PlayerId { get; set; }
    public Player? Player { get; set; }

    public DateTime AddedAt { get; set; } = DateTime.UtcNow;
}
