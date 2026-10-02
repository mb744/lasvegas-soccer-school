using Microsoft.EntityFrameworkCore;
using SoccerSchool.Api.Data;

namespace SoccerSchool.Api.Services;

/// <summary>
/// Archived families: those that deleted their account, or whose kids are all archived (a family
/// with no kids yet is active). Left out of messaging (every channel), chat, parent lists and
/// reports. Worked out from the players, so archiving or unarchiving a kid updates it with nothing
/// to keep in sync. Their records (registrations, invoices) stay.
/// </summary>
public static class FamilyArchive
{
    public static async Task<HashSet<int>> ArchivedIdsAsync(AppDbContext db, CancellationToken ct) =>
        (await db.ParentAccounts.AsNoTracking()
            // IgnoreQueryFilters: a.Players must include the archived kids for this test.
            .IgnoreQueryFilters()
            .Where(a => a.ReclaimEmailHash != null || (a.Players.Any() && a.Players.All(p => p.ArchivedAt != null)))
            .Select(a => a.Id)
            .ToListAsync(ct))
        .ToHashSet();

    /// <summary>Whether one family is archived (same rule as <see cref="ArchivedIdsAsync"/>).</summary>
    public static Task<bool> IsArchivedAsync(AppDbContext db, int familyId, CancellationToken ct) =>
        db.ParentAccounts.AsNoTracking().IgnoreQueryFilters()
            .AnyAsync(a => a.Id == familyId
                && (a.ReclaimEmailHash != null || (a.Players.Any() && a.Players.All(p => p.ArchivedAt != null))), ct);

    /// <summary>Addresses on deleted logins (scrubbed to this domain): never email them.</summary>
    public static bool IsDeletedLoginEmail(string? email) =>
        email is not null && email.EndsWith("@removed.lvss.local", StringComparison.OrdinalIgnoreCase);
}
