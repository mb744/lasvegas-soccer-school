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
    // Curated catalog. Order in each section matters — the admin sees these in this order
    // in the "Map to" dropdown when configuring a template variable. Group related fields
    // together (all tournament.*, all team.*, etc.) so the list scans.
    private static readonly TemplateProperty[] TournamentConfirmationProps = new[]
    {
        // --- Tournament / League ---
        new TemplateProperty("tournament.name", "Tournament / League name"),
        new TemplateProperty("tournament.kind", "Kind (\"Tournament\" or \"League\")"),
        new TemplateProperty("tournament.dates", "Dates (formatted range, e.g. Jun 5–12, 2026)"),
        new TemplateProperty("tournament.startDate", "Start date (Jun 5, 2026)"),
        new TemplateProperty("tournament.endDate", "End date (Jun 12, 2026)"),
        new TemplateProperty("tournament.startDateLong", "Start date — long form (June 5, 2026)"),
        new TemplateProperty("tournament.endDateLong", "End date — long form (June 12, 2026)"),
        new TemplateProperty("tournament.startDateShort", "Start date — short (06/05)"),
        new TemplateProperty("tournament.endDateShort", "End date — short (06/12)"),
        new TemplateProperty("tournament.startDayOfWeek", "Start day of week (Sunday)"),
        new TemplateProperty("tournament.endDayOfWeek", "End day of week (Sunday)"),
        new TemplateProperty("tournament.costPerPlayer", "Cost per player ($, formatted)"),
        new TemplateProperty("tournament.costPerPlayerPlain", "Cost per player — plain number (150.00)"),
        new TemplateProperty("tournament.totalCost", "Total tournament cost ($, formatted)"),
        new TemplateProperty("tournament.totalCostPlain", "Total cost — plain number (1500.00)"),
        // --- Team ---
        new TemplateProperty("team.name", "Team name"),
        // --- Player ---
        new TemplateProperty("player.firstName", "Player first name"),
        new TemplateProperty("player.lastName", "Player last name"),
        new TemplateProperty("player.fullName", "Player full name"),
        // --- Parent / guardian (primary account holder for the player) ---
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

    private static readonly TemplateProperty[] EventReminderProps = new[]
    {
        // --- Event ---
        new TemplateProperty("event.kind", "Kind (\"Game\", \"Practice\", or \"Event\")"),
        new TemplateProperty("event.summary", "Event summary (e.g. \"LVSS vs Albion\")"),
        new TemplateProperty("event.opponent", "Opponent name (games only)"),
        new TemplateProperty("event.isHome", "Home / Away / blank (games only)"),
        new TemplateProperty("event.dateTime", "Date + time (Sun, Jun 5, 2026 — 2:00 PM)"),
        new TemplateProperty("event.date", "Date (Jun 5, 2026)"),
        new TemplateProperty("event.dateLong", "Date — long form (June 5, 2026)"),
        new TemplateProperty("event.dateShort", "Date — short (06/05)"),
        new TemplateProperty("event.dayOfWeek", "Day of week (Sunday)"),
        new TemplateProperty("event.time", "Time (2:00 PM)"),
        new TemplateProperty("event.location", "Location / venue"),
        new TemplateProperty("event.description", "Description (admin notes)"),
        // --- Team ---
        new TemplateProperty("team.name", "Team name"),
        // --- App-level (admin settings) ---
        new TemplateProperty("app.zellePhone", "Zelle phone (Messaging → Settings)"),
        new TemplateProperty("app.activeSeason", "Active season (e.g. 2026/27)"),
        new TemplateProperty("app.publicBaseUrl", "Public site URL"),
    };

    // Cancellation reuses the same event/team/app set — the body wording is what differs,
    // not the available data.
    private static readonly TemplateProperty[] EventCancellationProps = EventReminderProps;

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
        TemplateContext.TournamentConfirmation => TournamentConfirmationProps,
        TemplateContext.EventReminder => EventReminderProps,
        TemplateContext.EventCancellation => EventCancellationProps,
        TemplateContext.MonthlyFee => MonthlyFeeProps,
        _ => Array.Empty<TemplateProperty>(),
    };
}
