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
/// it's required when the broadcast channel is Email and is ignored on SMS/WhatsApp sends.
/// <see cref="HasWhatsApp"/> mirrors the parent's stored flag — <c>null</c> when unknown
/// (ad-hoc/individual). The broadcast send loop skips WhatsApp sends to recipients with
/// <c>HasWhatsApp == false</c>.</summary>
public record ResolvedRecipient(string Phone, string? Name, int? ParentAccountId, Language? Language = null, string? Email = null, bool? HasWhatsApp = null);

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
                var indivPhone = target.Phone.Trim();
                // Look up the parent by phone (matching common variants) so the WhatsApp skip path
                // can short-circuit when the admin typed a known no-WhatsApp number directly.
                var indivHas = await LookupHasWhatsAppAsync(indivPhone, ct);
                return new RecipientList(
                    "Individual",
                    new[] { new ResolvedRecipient(indivPhone, target.Name?.Trim(), null, null, null, indivHas) });

            case RecipientTargetKind.CustomGroup:
                if (target.CustomGroupId is null)
                    return new RecipientList("Group", Array.Empty<ResolvedRecipient>());
                var group = await _db.MessageGroups
                    .Include(g => g.Members)
                    .FirstOrDefaultAsync(g => g.Id == target.CustomGroupId, ct);
                if (group is null) return new RecipientList("Group", Array.Empty<ResolvedRecipient>());
                // Look up HasWhatsApp from the linked ParentAccount for any member that has one.
                // Members added manually (no parent account) stay null = unknown.
                var memberParentIds = group.Members
                    .Where(m => m.ParentAccountId.HasValue)
                    .Select(m => m.ParentAccountId!.Value)
                    .Distinct()
                    .ToList();
                var whatsAppLookup = memberParentIds.Count == 0
                    ? new Dictionary<int, bool>()
                    : await _db.ParentAccounts
                        .Where(p => memberParentIds.Contains(p.Id))
                        .ToDictionaryAsync(p => p.Id, p => p.HasWhatsApp, ct);
                // Language is per-member (a group can mix EN and ES parents). The group's
                // own Language field is just the default we apply when adding new members.
                // Email is also per-member; broadcasts on the email channel skip members without one.
                var members = group.Members
                    .Where(m => !string.IsNullOrWhiteSpace(m.Phone) || !string.IsNullOrWhiteSpace(m.Email))
                    .Select(m => new ResolvedRecipient(
                        m.Phone, m.Name, m.ParentAccountId, m.Language, m.Email,
                        m.ParentAccountId.HasValue && whatsAppLookup.TryGetValue(m.ParentAccountId.Value, out var has)
                            ? (bool?)has : null))
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
                var listInputs = (target.AdHocRecipients ?? Array.Empty<ResolvedRecipient>())
                    .Where(r => !string.IsNullOrWhiteSpace(r.Phone) || !string.IsNullOrWhiteSpace(r.Email))
                    .Select(r => new
                    {
                        Phone = r.Phone?.Trim() ?? string.Empty,
                        Name = r.Name?.Trim(),
                        Email = string.IsNullOrWhiteSpace(r.Email) ? null : r.Email.Trim(),
                    })
                    .ToList();
                // One DB hit for all phone variants in the list — covers parents stored as the
                // typed form, the +1-prefixed form, or the bare-10-digit form.
                var allCandidates = listInputs
                    .Where(r => !string.IsNullOrWhiteSpace(r.Phone))
                    .SelectMany(r => PhoneVariants(r.Phone))
                    .Distinct()
                    .ToList();
                var adHocLookup = allCandidates.Count == 0
                    ? new Dictionary<string, bool>()
                    : await _db.ParentAccounts
                        .Where(p => p.CellPhone != null && allCandidates.Contains(p.CellPhone))
                        .ToDictionaryAsync(p => p.CellPhone!, p => p.HasWhatsApp, ct);
                var list = listInputs
                    .Select(r => new ResolvedRecipient(
                        r.Phone, r.Name, null, null, r.Email,
                        ResolveHasWhatsAppFromLookup(r.Phone, adHocLookup)))
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
            .Select(p => new { p.Id, p.CellPhone, p.FirstName, p.LastName, p.Language, p.HasWhatsApp, Email = p.User!.Email })
            .ToListAsync(ct);
        return rows
            .Select(r => new ResolvedRecipient(r.CellPhone ?? string.Empty, $"{r.FirstName} {r.LastName}".Trim(), r.Id, r.Language, r.Email, r.HasWhatsApp))
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
                r.ParentAccount.HasWhatsApp,
                Email = r.ParentAccount.User!.Email
            })
            .Distinct()
            .ToListAsync(ct);
        return rows
            .Select(r => new ResolvedRecipient(r.CellPhone ?? string.Empty, $"{r.FirstName} {r.LastName}".Trim(), r.Id, r.Language, r.Email, r.HasWhatsApp))
            .ToList();
    }

    /// <summary>One-shot lookup for a single typed phone. Tries common variants (as-typed,
    /// `+1`-prefixed, bare-10-digit) so the match works whether the admin pasted the E.164 form
    /// or just the digits.</summary>
    private async Task<bool?> LookupHasWhatsAppAsync(string phone, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(phone)) return null;
        var candidates = PhoneVariants(phone);
        var match = await _db.ParentAccounts
            .Where(p => p.CellPhone != null && candidates.Contains(p.CellPhone))
            .Select(p => (bool?)p.HasWhatsApp)
            .FirstOrDefaultAsync(ct);
        return match;
    }

    private static bool? ResolveHasWhatsAppFromLookup(string phone, IReadOnlyDictionary<string, bool> lookup)
    {
        if (string.IsNullOrWhiteSpace(phone)) return null;
        foreach (var v in PhoneVariants(phone))
            if (lookup.TryGetValue(v, out var has)) return has;
        return null;
    }

    /// <summary>Common form variants for a typed phone so DB equality matches stored E.164 numbers.
    /// Returns at most the as-typed value, a `+1`-prefixed variant for 10-digit US numbers, and a
    /// `+`-prefixed variant for 11-digit numbers starting with `1`.</summary>
    private static IReadOnlyList<string> PhoneVariants(string raw)
    {
        var trimmed = raw.Trim();
        var set = new HashSet<string>(StringComparer.Ordinal);
        if (!string.IsNullOrEmpty(trimmed)) set.Add(trimmed);
        var digits = new string(trimmed.Where(char.IsDigit).ToArray());
        if (digits.Length == 10) set.Add("+1" + digits);
        if (digits.Length == 11 && digits.StartsWith('1')) set.Add("+" + digits);
        if (digits.Length >= 10 && !trimmed.StartsWith('+')) set.Add("+" + digits);
        return set.ToList();
    }
}
