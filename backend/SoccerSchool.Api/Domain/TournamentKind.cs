namespace SoccerSchool.Api.Domain;

/// <summary>
/// What sort of multi-team competition a <see cref="Tournament"/> row represents. Both share the
/// same data shape (name + dates + costs + multi-team roster + GotSport sync + confirmations),
/// so they coexist on the same table; the only thing that varies is labeling in the UI and
/// the default cadence (tournaments are usually a single weekend; leagues are a multi-week
/// season).
/// </summary>
public enum TournamentKind
{
    Tournament = 0,
    League = 1,
}
