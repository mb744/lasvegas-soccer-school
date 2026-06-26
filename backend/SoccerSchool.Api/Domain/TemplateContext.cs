namespace SoccerSchool.Api.Domain;

/// <summary>
/// Tags a <see cref="WhatsAppTemplate"/> with the send pipeline it's meant for, so the admin's
/// per-variable "Map to property" picker can show the relevant property registry and the
/// hard-coded send flows (currently only TournamentConfirmation) can resolve variables by
/// property key rather than by fixed position.
///
/// FreeForm = the legacy default. The send pipeline treats variables as positional and the
/// admin fills them by hand in Compose (or via existing hard-coded fillers). Switching a
/// template to any other context unlocks the property-mapping UI for its variables.
/// </summary>
public enum TemplateContext
{
    FreeForm = 0,
    TournamentConfirmation = 1,
    EventReminder = 2,
    EventCancellation = 3,
    MonthlyFee = 4,
    /// <summary>Consolidated catalog covering tournaments, games, and practices in one place
    /// — every field across event details, dates/times, costs, opponent, team, player, parent,
    /// and app-level settings. Resolves whichever properties the surrounding broadcast can
    /// satisfy; the rest stay blank. Use this for new templates so the admin's "Map to" picker
    /// shows the full set; the per-context variants above are kept as aliases for templates
    /// that were tagged before the consolidation.</summary>
    EventDetails = 5,
    /// <summary>Invoice-flavored: per-invoice notification ("Your tournament fee is due").
    /// Resolves <c>invoice.*</c> (amount, description, due date, status, payment fields) plus
    /// the linked <c>chargeType.*</c>, <c>player.*</c>, <c>parent.*</c>, and <c>app.*</c>.
    /// Triggered from the Invoices admin row's "Email" action, which targets the invoice's
    /// parent and passes its id so the resolver can fill the invoice fields.</summary>
    InvoiceNotification = 6,
}
