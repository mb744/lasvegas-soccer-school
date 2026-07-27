namespace SoccerSchool.Api.Domain;

/// <summary>
/// Where a <see cref="HostedTournamentMatch"/> sits in the projected knockout bracket. Null
/// on round-robin group-stage matches; set on the four playoff slots the schedule generator
/// creates per tier that has exactly two brackets. Values 1–4 match the natural display order
/// (semifinals first, then consolation, then final) so a numeric sort produces the right
/// sequence in the public schedule.
/// </summary>
public enum PlayoffSlot
{
    SemifinalOne = 1,
    SemifinalTwo = 2,
    Consolation = 3,
    Final = 4,
}
