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
    private static readonly TemplateProperty[] TournamentConfirmationProps = new[]
    {
        new TemplateProperty("tournament.dates", "Tournament dates (formatted, e.g. Jun 15 – Jun 17, 2026)"),
        new TemplateProperty("tournament.startDate", "Tournament start date"),
        new TemplateProperty("tournament.endDate", "Tournament end date"),
        new TemplateProperty("tournament.name", "Tournament name"),
        new TemplateProperty("tournament.costPerPlayer", "Cost per player ($, formatted)"),
        new TemplateProperty("tournament.totalCost", "Total tournament cost ($, formatted)"),
        new TemplateProperty("team.name", "Team name"),
        new TemplateProperty("player.firstName", "Player first name"),
        new TemplateProperty("player.lastName", "Player last name"),
        new TemplateProperty("player.fullName", "Player full name"),
    };

    public static IReadOnlyList<TemplateProperty> ForContext(TemplateContext context) => context switch
    {
        TemplateContext.TournamentConfirmation => TournamentConfirmationProps,
        // Other contexts haven't been migrated to mapping yet (they still use hard-coded
        // fillers). Return an empty list so the admin sees no options + can't map.
        _ => Array.Empty<TemplateProperty>(),
    };
}
