using Microsoft.EntityFrameworkCore;
using SoccerSchool.Api.Data;
using SoccerSchool.Api.Domain;

namespace SoccerSchool.Api.Services;

/// <summary>
/// Archiving and unarchiving players. Archived players are hidden by the global query filter on
/// <see cref="Player"/>; these helpers change the flag. None of them save: the caller does.
/// </summary>
public static class PlayerArchive
{
    /// <summary>Archives the listed active players. Returns how many were archived.</summary>
    public static async Task<int> ArchiveAsync(AppDbContext db, IReadOnlyCollection<int> playerIds,
        PlayerArchiveReason reason, string? by, CancellationToken ct)
    {
        var players = await db.Players.Where(p => playerIds.Contains(p.Id)).ToListAsync(ct);
        var now = DateTime.UtcNow;
        foreach (var p in players)
        {
            p.ArchivedAt = now;
            p.ArchivedReason = reason;
            p.ArchivedBy = by;
        }
        return players.Count;
    }

    /// <summary>Brings the listed archived players back, onto the teams they were on. Returns how many.</summary>
    public static async Task<int> UnarchiveAsync(AppDbContext db, IReadOnlyCollection<int> playerIds, CancellationToken ct)
    {
        var players = await db.Players.IgnoreQueryFilters()
            .Where(p => playerIds.Contains(p.Id) && p.ArchivedAt != null)
            .ToListAsync(ct);
        foreach (var p in players) Clear(p);
        return players.Count;
    }

    /// <summary>A family deleted its account: archive its kids so they stop appearing in rosters,
    /// pickers and messaging. The records stay for the school.</summary>
    public static async Task ArchiveFamilyAsync(AppDbContext db, int parentAccountId, CancellationToken ct)
    {
        var ids = await db.Players.Where(p => p.ParentAccountId == parentAccountId).Select(p => p.Id).ToListAsync(ct);
        if (ids.Count > 0) await ArchiveAsync(db, ids, PlayerArchiveReason.FamilyDeleted, null, ct);
    }

    /// <summary>A deleted family signed up again: bring back the kids archived by the deletion
    /// (not ones an admin archived on purpose).</summary>
    public static async Task RestoreFamilyAsync(AppDbContext db, int parentAccountId, CancellationToken ct)
    {
        var players = await db.Players.IgnoreQueryFilters()
            .Where(p => p.ParentAccountId == parentAccountId && p.ArchivedAt != null
                        && p.ArchivedReason == PlayerArchiveReason.FamilyDeleted)
            .ToListAsync(ct);
        foreach (var p in players) Clear(p);
    }

    private static void Clear(Player p)
    {
        p.ArchivedAt = null;
        p.ArchivedReason = null;
        p.ArchivedBy = null;
    }
}
