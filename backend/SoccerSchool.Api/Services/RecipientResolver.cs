using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SoccerSchool.Api.Data;
using SoccerSchool.Api.Domain;
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

/// <summary>One concrete recipient row produced by the resolver. <see cref="Language"/> is
/// <c>null</c> when the recipient came from a context that doesn't carry a language preference
/// (ad-hoc paste list, individual phone, dynamic group). The broadcast layer fills nulls with
/// the request's default language at send time. <see cref="Email"/> is populated for curated
/// group members who have one set, and for active-season parents from <c>ApplicationUser.Email</c>;
/// it's required when the broadcast channel is Email and is ignored on SMS/WhatsApp sends.</summary>
public record ResolvedRecipient(string Phone, string? Name, int? ParentAccountId, Language? Language = null, string? Email = null);

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
                // Language is per-member (a group can mix EN and ES parents). The group's
                // own Language field is just the default we apply when adding new members.
                // Email is also per-member; broadcasts on the email channel skip members without one.
                var members = group.Members
                    .Where(m => !string.IsNullOrWhiteSpace(m.Phone) || !string.IsNullOrWhiteSpace(m.Email))
                    .Select(m => new ResolvedRecipient(m.Phone, m.Name, m.ParentAccountId, m.Language, m.Email))
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
                    .Where(r => !string.IsNullOrWhiteSpace(r.Phone) || !string.IsNullOrWhiteSpace(r.Email))
                    .Select(r => new ResolvedRecipient(
                        r.Phone?.Trim() ?? string.Empty,
                        r.Name?.Trim(),
                        null,
                        null,
                        string.IsNullOrWhiteSpace(r.Email) ? null : r.Email.Trim()))
                    .ToList();
                return new RecipientList($"Ad-hoc list ({list.Count})", list);

            default:
                return new RecipientList("Unknown target", Array.Empty<ResolvedRecipient>());
        }
    }

    private async Task<IReadOnlyList<ResolvedRecipient>> LoadAllParentsAsync(CancellationToken ct)
    {
        // Email comes from ApplicationUser (the parent's identity record). Parents missing both
        // phone and email are excluded outright — there's no way to reach them.
        var rows = await _db.ParentAccounts
            .Where(p => (p.CellPhone != null && p.CellPhone != "") || (p.User != null && p.User.Email != null && p.User.Email != ""))
            .Select(p => new { p.Id, p.CellPhone, p.FirstName, p.LastName, p.Language, Email = p.User!.Email })
            .ToListAsync(ct);
        return rows
            .Select(r => new ResolvedRecipient(r.CellPhone ?? string.Empty, $"{r.FirstName} {r.LastName}".Trim(), r.Id, r.Language, r.Email))
            .ToList();
    }

    private async Task<IReadOnlyList<ResolvedRecipient>> LoadActiveSeasonParentsAsync(CancellationToken ct)
    {
        var season = _app.ActiveSeason;
        var rows = await _db.Registrations
            .Where(r => r.Season == season && r.ParentAccount != null &&
                        ((r.ParentAccount.CellPhone != null && r.ParentAccount.CellPhone != "") ||
                         (r.ParentAccount.User != null && r.ParentAccount.User.Email != null && r.ParentAccount.User.Email != "")))
            .Select(r => new
            {
                r.ParentAccount!.Id,
                r.ParentAccount.CellPhone,
                r.ParentAccount.FirstName,
                r.ParentAccount.LastName,
                r.ParentAccount.Language,
                Email = r.ParentAccount.User!.Email
            })
            .Distinct()
            .ToListAsync(ct);
        return rows
            .Select(r => new ResolvedRecipient(r.CellPhone ?? string.Empty, $"{r.FirstName} {r.LastName}".Trim(), r.Id, r.Language, r.Email))
            .ToList();
    }
}
