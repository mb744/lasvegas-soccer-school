namespace SoccerSchool.Api.Domain;

/// <summary>
/// Distinguishes the different kinds of rows on the shared <see cref="ScheduledGame"/> table.
/// Games come from GotSport (with opponent + home/away); practices are admin-entered with just
/// date + location; miscellaneous covers one-off events (banquets, team photos, fundraisers)
/// that don't fit either bucket but still need a place on the team's schedule.
/// </summary>
public enum ScheduledEventKind
{
    Game = 0,
    Practice = 1,
    Miscellaneous = 2,
}
