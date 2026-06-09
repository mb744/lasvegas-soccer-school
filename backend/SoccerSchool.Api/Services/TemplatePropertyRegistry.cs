using SoccerSchool.Api.Domain;

namespace SoccerSchool.Api.Services;

/// <summary>One pickable property exposed in a template context — the key the send pipeline
/// resolves at fire time, plus the human-readable label the admin sees in the variable mapping
/// dropdown. Keep keys stable: stored in <see cref="WhatsAppTemplateVariable.PropertyKey"/>.</summary>
public record TemplateProperty(string Key, string Label);

/// <summary>
/// Static catalog of properties the send pipeline can resolve per <see cref="TemplateContext"/>.
/// Drives both the admin "Map to" dropdown on the Templates tab and the per-send resolver that
/// fills each variable from the context object (tournament/team/player/etc.).
///
/// To add a new property: add the key/label here, then teach the relevant context resolver
/// (e.g. <c>BuildTournamentProperties</c> in <c>MessagingController</c>) how to compute it.
/// </summary>
public static class TemplatePropertyRegistry
{
    // ============================================================================
    // EventDetails — the consolidated superset.
    //
    // Every event-flavored context (TournamentConfirmation / EventReminder /
    // EventCancellation) returns this list, so the admin sees the same dropdown
    // regardless of which legacy context a template was tagged with. The send-time
    // resolver fills whichever properties it can satisfy — fields the surrounding
    // broadcast can't fill (e.g. player.* on a non-per-player send) stay empty.
    // ============================================================================
    private static readonly TemplateProperty[] EventDetailsProps = new[]
    {
        // --- Event details (game / practice / misc one-off) ---
        new TemplateProperty("event.kind", "Event kind (\"Game\", \"Practice\", or \"Event\")"),
        new TemplateProperty("event.summary", "Event summary (e.g. \"LVSS vs Albion\")"),
        new TemplateProperty("event.opponent", "Opponent name (games only)"),
        new TemplateProperty("event.isHome", "Home / Away / blank (games only)"),
        new TemplateProperty("event.uniform", "Uniform / wear text (e.g. \"white jersey, blue shorts, blue socks\")"),
        new TemplateProperty("event.dateTime", "Date + time (Sun, Jun 5, 2026 — 2:00 PM)"),
        new TemplateProperty("event.date", "Date (Jun 5, 2026)"),
        new TemplateProperty("event.dateLong", "Date — long form (June 5, 2026)"),
        new TemplateProperty("event.dateShort", "Date — short (06/05)"),
        new TemplateProperty("event.dayOfWeek", "Day of week (Sunday)"),
        new TemplateProperty("event.time", "Time (2:00 PM)"),
        new TemplateProperty("event.location", "Location (venue \"Name, Address\" when set, else free text)"),
        new TemplateProperty("event.address", "Venue street address (when a venue is set)"),
        new TemplateProperty("event.field", "Field / location detail (free text, e.g. \"field 3\")"),
        new TemplateProperty("event.description", "Description (admin notes)"),
        // --- Tournament / League ---
        new TemplateProperty("tournament.name", "Tournament / League name"),
        new TemplateProperty("tournament.kind", "Kind (\"Tournament\" or \"League\")"),
        new TemplateProperty("tournament.dates", "Dates (formatted range, e.g. Jun 5–12, 2026)"),
        new TemplateProperty("tournament.startDate", "Start date (Jun 5, 2026)"),
        new TemplateProperty("tournament.endDate", "End date (Jun 12, 2026)"),
        new TemplateProperty("tournament.startDateLong", "Start date — long (June 5, 2026)"),
        new TemplateProperty("tournament.endDateLong", "End date — long (June 12, 2026)"),
        new TemplateProperty("tournament.startDateShort", "Start date — short (06/05)"),
        new TemplateProperty("tournament.endDateShort", "End date — short (06/12)"),
        new TemplateProperty("tournament.startDayOfWeek", "Start day of week (Sunday)"),
        new TemplateProperty("tournament.endDayOfWeek", "End day of week (Sunday)"),
        // --- Costs / fees ---
        new TemplateProperty("tournament.costPerPlayer", "Cost per player ($, formatted)"),
        new TemplateProperty("tournament.costPerPlayerPlain", "Cost per player — plain (150.00)"),
        new TemplateProperty("tournament.totalCost", "Total cost ($, formatted)"),
        new TemplateProperty("tournament.totalCostPlain", "Total cost — plain (1500.00)"),
        // --- Team ---
        new TemplateProperty("team.name", "Team name"),
        // --- Player (per-recipient — only filled on per-player sends) ---
        new TemplateProperty("player.firstName", "Player first name"),
        new TemplateProperty("player.lastName", "Player last name"),
        new TemplateProperty("player.fullName", "Player full name"),
        // --- Parent / guardian (per-recipient — primary account holder for the player) ---
        new TemplateProperty("parent.firstName", "Parent first name"),
        new TemplateProperty("parent.lastName", "Parent last name"),
        new TemplateProperty("parent.fullName", "Parent full name"),
        new TemplateProperty("parent.cellPhone", "Parent cell phone (E.164)"),
        new TemplateProperty("parent.email", "Parent email"),
        // --- App-level (admin settings) ---
        new TemplateProperty("app.zellePhone", "Zelle phone (Messaging → Settings)"),
        new TemplateProperty("app.activeSeason", "Active season (e.g. 2026/27)"),
        new TemplateProperty("app.publicBaseUrl", "Public site URL"),
    };

    private static readonly TemplateProperty[] MonthlyFeeProps = new[]
    {
        // --- Month ---
        new TemplateProperty("month.short", "Current month — short (Jun)"),
        new TemplateProperty("month.long", "Current month — long (June)"),
        new TemplateProperty("month.year", "Current year (2026)"),
        new TemplateProperty("month.number", "Month number (06)"),
        new TemplateProperty("month.label", "Month + year (June 2026)"),
        // --- App-level (admin settings) ---
        new TemplateProperty("app.zellePhone", "Zelle phone (Messaging → Settings)"),
        new TemplateProperty("app.activeSeason", "Active season (e.g. 2026/27)"),
        new TemplateProperty("app.publicBaseUrl", "Public site URL"),
    };

    public static IReadOnlyList<TemplateProperty> ForContext(TemplateContext context) => context switch
    {
        // Legacy + consolidated event-flavored contexts all share the same catalog.
        TemplateContext.EventDetails => EventDetailsProps,
        TemplateContext.TournamentConfirmation => EventDetailsProps,
        TemplateContext.EventReminder => EventDetailsProps,
        TemplateContext.EventCancellation => EventDetailsProps,
        TemplateContext.MonthlyFee => MonthlyFeeProps,
        _ => Array.Empty<TemplateProperty>(),
    };
}
