namespace SoccerSchool.Api.Domain;

/// <summary>
/// Join row linking a <see cref="HostedTournament"/> to either an LVSS <see cref="Team"/> or an
/// external <see cref="InvitedTeam"/>. Exactly one of <see cref="LvssTeamId"/> or
/// <see cref="InvitedTeamId"/> is set — enforced at the controller since SQL Server doesn't do
/// XOR check constraints cleanly across nullable FKs. Both FKs are SetNull-safe (via
/// ClientSetNull) so deleting a Team/InvitedTeam doesn't wipe the roster row's history — the
/// admin can see "team since removed" instead of losing the participation record entirely.
/// </summary>
public class HostedTournamentTeam
{
    public int Id { get; set; }

    public int HostedTournamentId { get; set; }
    public HostedTournament? HostedTournament { get; set; }

    /// <summary>Set when this row is one of our own teams.</summary>
    public int? LvssTeamId { get; set; }
    public Team? LvssTeam { get; set; }

    /// <summary>Set when this row is an external invited team.</summary>
    public int? InvitedTeamId { get; set; }
    public InvitedTeam? InvitedTeam { get; set; }

    /// <summary>Per-row notes (bracket assignment, group letter, seed, …). Kept separate from
    /// the InvitedTeam's own notes so per-tournament context doesn't bleed into the catalog.</summary>
    [System.ComponentModel.DataAnnotations.MaxLength(500)]
    public string? Notes { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
