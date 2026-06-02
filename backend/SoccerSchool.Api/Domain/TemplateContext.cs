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
}
