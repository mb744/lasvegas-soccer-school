using System.ComponentModel.DataAnnotations;

namespace SoccerSchool.Api.Domain;

/// <summary>
/// One playing field the event will use — either picked from the venue's <see cref="VenueField"/>
/// catalog (VenueFieldId set) or ad-hoc typed in per event. Name is required either way so the
/// public schedule can label matches without joining back to the venue catalog.
/// </summary>
public class HostedTournamentField
{
    public int Id { get; set; }

    public int HostedTournamentId { get; set; }
    public HostedTournament? HostedTournament { get; set; }

    /// <summary>Optional link to the venue's field catalog. Null when the admin typed an
    /// ad-hoc name for this event only.</summary>
    public int? VenueFieldId { get; set; }
    public VenueField? VenueField { get; set; }

    [Required, MaxLength(80)]
    public string Name { get; set; } = string.Empty;

    public int SortOrder { get; set; }

    [MaxLength(500)]
    public string? Notes { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
