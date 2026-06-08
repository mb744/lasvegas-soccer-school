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

    public static IReadOnlyList<TemplateProperty> ForContext(TemplateContext context) => context switch
    {
        TemplateContext.TournamentConfirmation => TournamentConfirmationProps,
        // Other contexts haven't been migrated to mapping yet (they still use hard-coded
        // fillers). Return an empty list so the admin sees no options + can't map.
        _ => Array.Empty<TemplateProperty>(),
    };
}
