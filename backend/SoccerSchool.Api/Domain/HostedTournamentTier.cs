using System.ComponentModel.DataAnnotations;

namespace SoccerSchool.Api.Domain;

/// <summary>
/// A named bracket/division inside a <see cref="HostedTournament"/> — "U10 A", "U12 B",
/// "Gold", etc. Teams on the event get slotted into a tier via
/// <see cref="HostedTournamentTeam.TierId"/>. Tiers cascade-delete with their parent event;
/// deleting a tier alone leaves its member teams on the event with TierId nulled (SetNull via
/// ClientSetNull so the participation record survives).
/// </summary>
public class HostedTournamentTier
{
    public int Id { get; set; }

    public int HostedTournamentId { get; set; }
    public HostedTournament? HostedTournament { get; set; }

    [Required, MaxLength(80)]
    public string Name { get; set; } = string.Empty;

    /// <summary>Sort key for stable display order. Admin can reorder; default = insertion order.</summary>
    public int SortOrder { get; set; }

    [MaxLength(500)]
    public string? Notes { get; set; }

    /// <summary>When true the scheduler pairs teams from DIFFERENT brackets in this tier (only —
    /// no intra-bracket matches). When false teams only play others in their own bracket
    /// (standard round-robin per bracket).</summary>
    public bool CrossBracketPlay { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public List<HostedTournamentBracket> Brackets { get; set; } = new();
}
