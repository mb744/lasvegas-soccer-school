using System.ComponentModel.DataAnnotations;

namespace SoccerSchool.Api.Domain;

/// <summary>
/// One event pulled from a team's external calendar feed. Mapped from the standard ICS fields:
/// SUMMARY → Summary, DTSTART → StartsAt, DTEND → EndsAt, LOCATION → Location, UID → ExternalUid.
/// Re-syncs upsert on <see cref="ExternalUid"/> so cancellations/re-scheduling come through cleanly.
/// </summary>
public class ScheduledGame
{
    public int Id { get; set; }

    public int TeamId { get; set; }
    public Team? Team { get; set; }

    /// <summary>Game (scraped from GotSport, has opponent + home/away) or Practice (admin-entered,
    /// no opponent). Drives template-picker auto-selection in Compose.</summary>
    public ScheduledEventKind Kind { get; set; } = ScheduledEventKind.Game;

    /// <summary>The ICS UID property — the calendar event's stable identifier. Used as the upsert
    /// key so a re-sync updates the existing row rather than creating duplicates. For manually-
    /// added practices, set to a generated value (e.g. <c>practice-{guid}</c>) so the unique
    /// (team, ExternalUid) index doesn't collide.</summary>
    [Required, MaxLength(256)]
    public string ExternalUid { get; set; } = string.Empty;

    public DateTime StartsAt { get; set; }
    public DateTime? EndsAt { get; set; }

    /// <summary>SUMMARY field — typically "{team} vs {opponent}" or "Practice — {field}" depending
    /// on how the feed source formats it. Surfaced to the admin in the game picker.</summary>
    [MaxLength(512)]
    public string? Summary { get; set; }

    [MaxLength(512)]
    public string? Location { get; set; }

    [MaxLength(2000)]
    public string? Description { get; set; }

    /// <summary>Opponent name from the scraped page. Null for non-match events (practices etc.).
    /// Used to build the "Game vs &lt;opponent&gt;" autofill in the Compose tab.</summary>
    [MaxLength(256)]
    public string? OpponentName { get; set; }

    /// <summary>True = our team is the home team for this match, false = away, null = unknown
    /// (practice, training, anything that doesn't pair us against an opponent). Drives the
    /// wear-text autofill: home → "white jersey, blue shorts, blue socks"; away → "all blue".</summary>
    public bool? IsHome { get; set; }

    /// <summary>Updated on every sync. Lets us detect events that disappeared from the feed
    /// (cancelled) by finding games whose LastSeenAt is older than the team's LastSyncedAt.</summary>
    public DateTime LastSeenAt { get; set; } = DateTime.UtcNow;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
