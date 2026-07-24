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

    /// <summary>Optional tier / division assignment inside the event. Null means "not yet
    /// bracketed". SetNull (via ClientSetNull) on tier delete so removing a tier alone doesn't
    /// pull the team off the event.</summary>
    public int? TierId { get; set; }
    public HostedTournamentTier? Tier { get; set; }

    /// <summary>Optional bracket assignment within the tier. When set, the controller keeps
    /// <see cref="TierId"/> in sync with the bracket's owning tier so downstream queries can
    /// filter by tier without loading the bracket join.</summary>
    public int? BracketId { get; set; }
    public HostedTournamentBracket? Bracket { get; set; }

    /// <summary>Whether this team has paid the per-team fee. Toggled from the roster row so
    /// admin can track collection without leaving the Hosted Tournaments page.</summary>
    public bool Paid { get; set; }

    /// <summary>Stamped when Paid flips to true; cleared when it flips back off.</summary>
    public DateTime? PaidAt { get; set; }

    /// <summary>Admin-typed "how did they pay" — Zelle, Cash, Check #1234, etc.</summary>
    [System.ComponentModel.DataAnnotations.MaxLength(120)]
    public string? PaymentMethod { get; set; }

    /// <summary>Confirmation / check number / Stripe id, optional.</summary>
    [System.ComponentModel.DataAnnotations.MaxLength(120)]
    public string? PaymentReference { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
