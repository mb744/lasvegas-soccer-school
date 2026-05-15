using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SoccerSchool.Api.Data;
using SoccerSchool.Api.Options;

namespace SoccerSchool.Api.Services;

/// <summary>
/// Turns an abstract "who should receive this" descriptor (individual / curated group / dynamic group)
/// into a concrete list of phone numbers. Dynamic groups are resolved at send time from registration
/// data, so they always reflect the current state — no stored membership list to keep in sync.
/// </summary>
public interface IRecipientResolver
{
    Task<RecipientList> ResolveAsync(RecipientTarget target, CancellationToken ct);
    Task<IReadOnlyList<DynamicGroupSummary>> ListDynamicGroupsAsync(CancellationToken ct);
}

public record ResolvedRecipient(string Phone, string? Name, int? ParentAccountId);

public record RecipientList(string Label, IReadOnlyList<ResolvedRecipient> Recipients);

public record DynamicGroupSummary(string Key, string Label, int Count);

public record RecipientTarget(
    RecipientTargetKind Kind,
    string? Phone = null,
    string? Name = null,
    int? CustomGroupId = null,
    string? DynamicGroupKey = null,
    IReadOnlyList<ResolvedRecipient>? AdHocRecipients = null);

public enum RecipientTargetKind
{
    Individual = 0,
    CustomGroup = 1,
    DynamicGroup = 2,
    /// <summary>One-off list of phones the admin pasted/typed at compose time. Not persisted.
    /// Useful for blasting an existing WhatsApp group's members without first creating a curated
    /// group, since WhatsApp's Business API can't address real WhatsApp group chats.</summary>
    AdHocList = 3
}

public class RecipientResolver : IRecipientResolver
{
    public const string DynamicAllParents = "all-parents";
    public const string DynamicActiveSeasonParents = "active-season-parents";

    private readonly AppDbContext _db;
    private readonly AppOptions _app;

    public RecipientResolver(AppDbContext db, IOptions<AppOptions> app)
    {
        _db = db;
        _app = app.Value;
    }

    public async Task<IReadOnlyList<DynamicGroupSummary>> ListDynamicGroupsAsync(CancellationToken ct)
    {
        var allCount = await _db.ParentAccounts
            .Where(p => p.CellPhone != null && p.CellPhone != "")
            .CountAsync(ct);

        var season = _app.ActiveSeason;
        var activeCount = await _db.Registrations
            .Where(r => r.Season == season && r.ParentAccount != null && r.ParentAccount.CellPhone != null && r.ParentAccount.CellPhone != "")
            .Select(r => r.ParentAccountId)
            .Distinct()
            .CountAsync(ct);

        return new[]
        {
            new DynamicGroupSummary(DynamicAllParents, "All parents with a phone on file", allCount),
            new DynamicGroupSummary(DynamicActiveSeasonParents, $"Parents registered in {season}", activeCount)
        };
    }

    public async Task<RecipientList> ResolveAsync(RecipientTarget target, CancellationToken ct)
    {
        switch (target.Kind)
        {
            case RecipientTargetKind.Individual:
                if (string.IsNullOrWhiteSpace(target.Phone))
                    return new RecipientList("Individual", Array.Empty<ResolvedRecipient>());
                return new RecipientList(
                    "Individual",
                    new[] { new ResolvedRecipient(target.Phone.Trim(), target.Name?.Trim(), null) });

            case RecipientTargetKind.CustomGroup:
                if (target.CustomGroupId is null)
                    return new RecipientList("Group", Array.Empty<ResolvedRecipient>());
                var group = await _db.MessageGroups
                    .Include(g => g.Members)
                    .FirstOrDefaultAsync(g => g.Id == target.CustomGroupId, ct);
                if (group is null) return new RecipientList("Group", Array.Empty<ResolvedRecipient>());
                var members = group.Members
                    .Where(m => !string.IsNullOrWhiteSpace(m.Phone))
                    .Select(m => new ResolvedRecipient(m.Phone, m.Name, m.ParentAccountId))
                    .ToList();
                return new RecipientList($"Group: {group.Name}", members);

            case RecipientTargetKind.DynamicGroup:
                return target.DynamicGroupKey switch
                {
                    DynamicAllParents =>
                        new RecipientList("All parents with a phone on file", await LoadAllParentsAsync(ct)),
                    DynamicActiveSeasonParents =>
                        new RecipientList($"Parents registered in {_app.ActiveSeason}", await LoadActiveSeasonParentsAsync(ct)),
                    _ => new RecipientList("Unknown group", Array.Empty<ResolvedRecipient>())
                };

            case RecipientTargetKind.AdHocList:
                var list = (target.AdHocRecipients ?? Array.Empty<ResolvedRecipient>())
                    .Where(r => !string.IsNullOrWhiteSpace(r.Phone))
                    .Select(r => new ResolvedRecipient(r.Phone.Trim(), r.Name?.Trim(), null))
                    .ToList();
                return new RecipientList($"Ad-hoc list ({list.Count})", list);

            default:
                return new RecipientList("Unknown target", Array.Empty<ResolvedRecipient>());
        }
    }

    private async Task<IReadOnlyList<ResolvedRecipient>> LoadAllParentsAsync(CancellationToken ct)
    {
        var rows = await _db.ParentAccounts
            .Where(p => p.CellPhone != null && p.CellPhone != "")
            .Select(p => new { p.Id, p.CellPhone, p.FirstName, p.LastName })
            .ToListAsync(ct);
        return rows
            .Select(r => new ResolvedRecipient(r.CellPhone!, $"{r.FirstName} {r.LastName}".Trim(), r.Id))
            .ToList();
    }

    private async Task<IReadOnlyList<ResolvedRecipient>> LoadActiveSeasonParentsAsync(CancellationToken ct)
    {
        var season = _app.ActiveSeason;
        var rows = await _db.Registrations
            .Where(r => r.Season == season && r.ParentAccount != null && r.ParentAccount.CellPhone != null && r.ParentAccount.CellPhone != "")
            .Select(r => new { r.ParentAccount!.Id, r.ParentAccount.CellPhone, r.ParentAccount.FirstName, r.ParentAccount.LastName })
            .Distinct()
            .ToListAsync(ct);
        return rows
            .Select(r => new ResolvedRecipient(r.CellPhone!, $"{r.FirstName} {r.LastName}".Trim(), r.Id))
            .ToList();
    }
}
