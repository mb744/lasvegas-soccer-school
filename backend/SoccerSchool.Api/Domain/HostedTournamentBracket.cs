using System.ComponentModel.DataAnnotations;

namespace SoccerSchool.Api.Domain;

/// <summary>
/// A named sub-group inside a <see cref="HostedTournamentTier"/> — "Group A", "Pool 1", etc.
/// Teams are slotted directly into brackets via <see cref="HostedTournamentTeam.BracketId"/>.
/// Scheduling behavior branches on the parent tier's <see cref="HostedTournamentTier.CrossBracketPlay"/>
/// flag: when false, matches happen inside each bracket (round-robin); when true, teams from one
/// bracket only play teams from OTHER brackets in the same tier.
/// </summary>
public class HostedTournamentBracket
{
    public int Id { get; set; }

    public int TierId { get; set; }
    public HostedTournamentTier? Tier { get; set; }

    [Required, MaxLength(80)]
    public string Name { get; set; } = string.Empty;

    public int SortOrder { get; set; }

    [MaxLength(500)]
    public string? Notes { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
