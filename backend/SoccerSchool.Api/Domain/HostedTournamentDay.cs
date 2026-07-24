using System.ComponentModel.DataAnnotations;

namespace SoccerSchool.Api.Domain;

/// <summary>
/// A single scheduling window inside a <see cref="HostedTournament"/> — one row per calendar
/// date the event runs, with an optional start + end time. Populated separately from the
/// event's overall StartDate/EndDate so multi-day events can carry per-day time ranges
/// ("Sat 8:00 AM – 5:00 PM, Sun 9:00 AM – 12:00 PM"). Cascade-deletes with the parent event.
/// </summary>
public class HostedTournamentDay
{
    public int Id { get; set; }

    public int HostedTournamentId { get; set; }
    public HostedTournament? HostedTournament { get; set; }

    public DateOnly Date { get; set; }

    /// <summary>First slot of the day. Null when the admin hasn't published a schedule yet.</summary>
    public TimeOnly? StartTime { get; set; }

    /// <summary>Last slot end of the day. Null when only a start is set.</summary>
    public TimeOnly? EndTime { get; set; }

    [MaxLength(500)]
    public string? Notes { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
