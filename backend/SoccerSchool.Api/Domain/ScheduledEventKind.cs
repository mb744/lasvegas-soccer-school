namespace SoccerSchool.Api.Domain;

/// <summary>
/// Distinguishes scraped games from manually-added practices on the same <see cref="ScheduledGame"/>
/// table. Games come from GotSport (with opponent + home/away); practices are admin-entered with
/// just date + location.
/// </summary>
public enum ScheduledEventKind
{
    Game = 0,
    Practice = 1
}
