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

/// <summary>Tiny per-family flags projection used inside the curated-group resolver to filter
/// out members linked to a family that's opted out of communications.</summary>
internal record FamilyFlags(int Id, bool HasWhatsApp, bool NoCommunications);

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
    public const string DynamicTrialOverParents = "trial-over-parents";

    /// <summary>Prefix for per-team dynamic groups: key <c>team-{id}</c> resolves to the parents of
    /// that team's roster players. Lets the Compose tab target a team's roster with the existing
    /// broadcast pipeline, with the audience always reflecting the current roster.</summary>
    public const string DynamicTeamPrefix = "team-";

    /// <summary>Internal target for the event re-send flow: key <c>event-pending-{eventId}</c>
    /// resolves to the guardians of that event's rostered players who haven't confirmed yet (no
    /// attendance row or status Pending). Not listed as a pickable group.</summary>
    public const string DynamicEventPendingPrefix = "event-pending-";

    /// <summary>Internal target for the per-player re-send: key <c>player-{playerId}</c> resolves to
    /// that one player's guardians. Not listed as a pickable group.</summary>
    public const string DynamicPlayerPrefix = "player-";

    private readonly AppDbContext _db;
    private readonly AppOptions _app;

    public RecipientResolver(AppDbContext db, IOptions<AppOptions> app)
    {
        _db = db;
        _app = app.Value;
    }

    public async Task<IReadOnlyList<DynamicGroupSummary>> ListDynamicGroupsAsync(CancellationToken ct)
    {
        var season = _app.ActiveSeason;

        // Counts come from the same resolvers used at send time, so the picker shows exactly who will
        // be reached — including every additional parent/guardian on each family's account, deduped
        // by phone/email. Keeping these in sync with the loaders avoids the picker undercounting.
        var allCount = (await LoadAllParentsAsync(ct)).Count;
        var activeCount = (await LoadActiveSeasonParentsAsync(ct)).Count;
        var trialOverCount = (await LoadTrialOverParentsAsync(ct)).Count;

        var teams = await _db.Teams
            .Where(t => t.Roster.Any())
            .OrderBy(t => t.Name)
            .Select(t => new { t.Id, t.Name })
            .ToListAsync(ct);

        var result = new List<DynamicGroupSummary>
        {
            new(DynamicAllParents, "All parents/guardians on file", allCount),
            new(DynamicActiveSeasonParents, $"Parents/guardians registered in {season}", activeCount),
            new(DynamicTrialOverParents, "Parents/guardians whose free trial is over", trialOverCount)
        };
        foreach (var t in teams)
        {
            var recipients = await LoadTeamRosterParentsAsync(t.Id, ct);
            result.Add(new DynamicGroupSummary($"{DynamicTeamPrefix}{t.Id}", $"Team: {t.Name}", recipients.Recipients.Count));
        }
        return result;
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
                // Pull HasWhatsApp + the family no-comms flag in one trip. Members linked to
                // a flagged ParentAccount are filtered out below regardless of their group row.
                var familyRows = memberParentIds.Count == 0
                    ? new List<FamilyFlags>()
                    : await _db.ParentAccounts
                        .Where(p => memberParentIds.Contains(p.Id))
                        .Select(p => new FamilyFlags(p.Id, p.HasWhatsApp, p.NoCommunications))
                        .ToListAsync(ct);
                var familyLookup = familyRows.ToDictionary(f => f.Id, f => f);
                // Language is per-member (a group can mix EN and ES parents). The group's
                // own Language field is just the default we apply when adding new members.
                // Email is also per-member; broadcasts on the email channel skip members without one.
                var members = group.Members
                    .Where(m => !string.IsNullOrWhiteSpace(m.Phone) || !string.IsNullOrWhiteSpace(m.Email))
                    .Where(m => !(m.ParentAccountId.HasValue
                        && familyLookup.TryGetValue(m.ParentAccountId.Value, out var fam)
                        && fam.NoCommunications))
                    .Select(m => new ResolvedRecipient(
                        m.Phone, m.Name, m.ParentAccountId, m.Language, m.Email,
                        m.ParentAccountId.HasValue && familyLookup.TryGetValue(m.ParentAccountId.Value, out var has)
                            ? (bool?)has.HasWhatsApp : null))
                    .ToList();
                return new RecipientList($"Group: {group.Name}", members);

            case RecipientTargetKind.DynamicGroup:
                if (target.DynamicGroupKey is string teamKey
                    && teamKey.StartsWith(DynamicTeamPrefix, StringComparison.Ordinal)
                    && int.TryParse(teamKey.AsSpan(DynamicTeamPrefix.Length), out var teamId))
                {
                    return await LoadTeamRosterParentsAsync(teamId, ct);
                }
                if (target.DynamicGroupKey is string pendKey
                    && pendKey.StartsWith(DynamicEventPendingPrefix, StringComparison.Ordinal)
                    && int.TryParse(pendKey.AsSpan(DynamicEventPendingPrefix.Length), out var pendingEventId))
                {
                    return await LoadEventPendingGuardiansAsync(pendingEventId, ct);
                }
                if (target.DynamicGroupKey is string playerKey
                    && playerKey.StartsWith(DynamicPlayerPrefix, StringComparison.Ordinal)
                    && int.TryParse(playerKey.AsSpan(DynamicPlayerPrefix.Length), out var singlePlayerId))
                {
                    return await LoadPlayerGuardiansAsync(singlePlayerId, ct);
                }
                return target.DynamicGroupKey switch
                {
                    DynamicAllParents =>
                        new RecipientList("All parents with a phone on file", await LoadAllParentsAsync(ct)),
                    DynamicActiveSeasonParents =>
                        new RecipientList($"Parents registered in {_app.ActiveSeason}", await LoadActiveSeasonParentsAsync(ct)),
                    DynamicTrialOverParents =>
                        new RecipientList("Parents whose free trial is over", await LoadTrialOverParentsAsync(ct)),
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
                    .SelectMany(r => PhoneNormalizer.Variants(r.Phone))
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
            .Where(p => !p.NoCommunications &&
                ((p.CellPhone != null && p.CellPhone != "") || (p.User != null && p.User.Email != null && p.User.Email != "")))
            .Select(p => new { p.Id, p.CellPhone, p.FirstName, p.LastName, p.Language, p.HasWhatsApp, Email = p.User!.Email })
            .ToListAsync(ct);
        var parents = rows
            .Select(r => new ResolvedRecipient(r.CellPhone ?? string.Empty, $"{r.FirstName} {r.LastName}".Trim(), r.Id, r.Language, r.Email, r.HasWhatsApp))
            .ToList();
        // null = every account's contacts, matching the "all parents" scope.
        var contacts = await LoadContactsAsync(null, ct);
        return DedupeByReachability(parents.Concat(contacts));
    }

    private async Task<IReadOnlyList<ResolvedRecipient>> LoadActiveSeasonParentsAsync(CancellationToken ct)
    {
        var season = _app.ActiveSeason;
        var rows = await _db.Registrations
            .Where(r => r.Season == season && r.ParentAccount != null && !r.ParentAccount.NoCommunications &&
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
        var parents = rows
            .Select(r => new ResolvedRecipient(r.CellPhone ?? string.Empty, $"{r.FirstName} {r.LastName}".Trim(), r.Id, r.Language, r.Email, r.HasWhatsApp))
            .ToList();
        var contacts = await LoadContactsAsync(rows.Select(r => r.Id).Distinct().ToList(), ct);
        return DedupeByReachability(parents.Concat(contacts));
    }

    private async Task<IReadOnlyList<ResolvedRecipient>> LoadTrialOverParentsAsync(CancellationToken ct)
    {
        // Distinct parents in the active season whose at-least-one player has FreeTrialOver = true.
        // Joining through RegistrationPlayer → Registration → ParentAccount; dedupe on parent id so
        // a family with multiple post-trial kids only gets one fee notification.
        var season = _app.ActiveSeason;
        var rows = await _db.RegistrationPlayers
            .Where(rp => rp.FreeTrialOver
                && rp.Registration!.Season == season
                && rp.Registration.ParentAccount != null
                && !rp.Registration.ParentAccount.NoCommunications
                && ((rp.Registration.ParentAccount.CellPhone != null && rp.Registration.ParentAccount.CellPhone != "")
                    || (rp.Registration.ParentAccount.User != null && rp.Registration.ParentAccount.User.Email != null && rp.Registration.ParentAccount.User.Email != "")))
            .Select(rp => new
            {
                rp.Registration!.ParentAccount!.Id,
                rp.Registration.ParentAccount.CellPhone,
                rp.Registration.ParentAccount.FirstName,
                rp.Registration.ParentAccount.LastName,
                rp.Registration.ParentAccount.Language,
                rp.Registration.ParentAccount.HasWhatsApp,
                Email = rp.Registration.ParentAccount.User!.Email
            })
            .Distinct()
            .ToListAsync(ct);
        var parents = rows
            .Select(r => new ResolvedRecipient(
                r.CellPhone ?? string.Empty,
                $"{r.FirstName} {r.LastName}".Trim(),
                r.Id,
                r.Language,
                r.Email,
                r.HasWhatsApp))
            .ToList();
        var contacts = await LoadContactsAsync(rows.Select(r => r.Id).Distinct().ToList(), ct);
        return DedupeByReachability(parents.Concat(contacts));
    }

    /// <summary>Parents of a team's roster players (deduped per parent), for the <c>team-{id}</c>
    /// dynamic group. Mirrors <see cref="LoadActiveSeasonParentsAsync"/>: only parents reachable by
    /// phone or email are included, and language/HasWhatsApp come from the parent profile.</summary>
    private async Task<RecipientList> LoadTeamRosterParentsAsync(int teamId, CancellationToken ct)
    {
        var team = await _db.Teams
            .Where(t => t.Id == teamId)
            .Select(t => new { t.Name })
            .FirstOrDefaultAsync(ct);
        if (team is null) return new RecipientList("Unknown team", Array.Empty<ResolvedRecipient>());

        var rows = await _db.TeamPlayers
            .Where(tp => tp.TeamId == teamId
                && tp.Player!.ParentAccount != null
                && !tp.Player.ParentAccount.NoCommunications
                && ((tp.Player.ParentAccount.CellPhone != null && tp.Player.ParentAccount.CellPhone != "")
                    || (tp.Player.ParentAccount.User != null && tp.Player.ParentAccount.User.Email != null && tp.Player.ParentAccount.User.Email != "")))
            .Select(tp => new
            {
                tp.Player!.ParentAccount!.Id,
                tp.Player.ParentAccount.CellPhone,
                tp.Player.ParentAccount.FirstName,
                tp.Player.ParentAccount.LastName,
                tp.Player.ParentAccount.Language,
                tp.Player.ParentAccount.HasWhatsApp,
                Email = tp.Player.ParentAccount.User!.Email
            })
            .Distinct()
            .ToListAsync(ct);

        var parents = rows
            .Select(r => new ResolvedRecipient(
                r.CellPhone ?? string.Empty, $"{r.FirstName} {r.LastName}".Trim(), r.Id, r.Language, r.Email, r.HasWhatsApp))
            .ToList();
        var contacts = await LoadContactsAsync(rows.Select(r => r.Id).Distinct().ToList(), ct);

        // Coaches: included with every team-{id} send. ParentAccountId is null (they aren't
        // tied to a parent account); dedup falls back to phone/email so a coach who shares a
        // number with a parent won't get a double-send.
        var coaches = await _db.TeamCoaches
            .Where(c => c.TeamId == teamId
                && ((c.Phone != null && c.Phone != "") || (c.Email != null && c.Email != "")))
            .Select(c => new { c.Name, c.Phone, c.Email, c.Language, c.HasWhatsApp })
            .ToListAsync(ct);
        var coachRecipients = coaches
            .Select(c => new ResolvedRecipient(
                c.Phone ?? string.Empty, c.Name, null, c.Language, c.Email, c.HasWhatsApp))
            .ToList();

        return new RecipientList($"Team: {team.Name}", DedupeByReachability(parents.Concat(contacts).Concat(coachRecipients)));
    }

    /// <summary>Guardians of an event's rostered players who haven't confirmed (no attendance row
    /// or status Pending). Used by the "re-send to no-shows" flow; carries per-recipient language.</summary>
    private async Task<RecipientList> LoadEventPendingGuardiansAsync(int eventId, CancellationToken ct)
    {
        var teamId = await _db.ScheduledGames
            .Where(g => g.Id == eventId)
            .Select(g => (int?)g.TeamId)
            .FirstOrDefaultAsync(ct);
        if (teamId is null) return new RecipientList("Unknown event", Array.Empty<ResolvedRecipient>());

        var rosterPlayerIds = await _db.TeamPlayers
            .Where(tp => tp.TeamId == teamId)
            .Select(tp => tp.PlayerId)
            .ToListAsync(ct);
        // Players who explicitly have a non-Pending status are excluded; everyone else is "no response".
        var answeredPlayerIds = await _db.EventAttendances
            .Where(a => a.ScheduledGameId == eventId && a.Status != AttendanceStatus.Pending)
            .Select(a => a.PlayerId)
            .ToListAsync(ct);
        var pendingPlayerIds = rosterPlayerIds.Except(answeredPlayerIds).ToList();
        if (pendingPlayerIds.Count == 0) return new RecipientList("No-response guardians", Array.Empty<ResolvedRecipient>());

        var accountIds = await _db.Players
            .Where(p => pendingPlayerIds.Contains(p.Id))
            .Select(p => p.ParentAccountId)
            .Distinct()
            .ToListAsync(ct);

        var rows = await _db.ParentAccounts
            .Where(p => accountIds.Contains(p.Id)
                && !p.NoCommunications
                && ((p.CellPhone != null && p.CellPhone != "")
                    || (p.User != null && p.User.Email != null && p.User.Email != "")))
            .Select(p => new { p.Id, p.CellPhone, p.FirstName, p.LastName, p.Language, p.HasWhatsApp, Email = p.User!.Email })
            .ToListAsync(ct);
        var parents = rows
            .Select(r => new ResolvedRecipient(
                r.CellPhone ?? string.Empty, $"{r.FirstName} {r.LastName}".Trim(), r.Id, r.Language, r.Email, r.HasWhatsApp))
            .ToList();
        var contacts = await LoadContactsAsync(accountIds, ct);
        return new RecipientList("No-response guardians", DedupeByReachability(parents.Concat(contacts)));
    }

    /// <summary>One player's guardians (primary parent + contacts), for the per-player re-send.</summary>
    private async Task<RecipientList> LoadPlayerGuardiansAsync(int playerId, CancellationToken ct)
    {
        var accountId = await _db.Players
            .Where(p => p.Id == playerId)
            .Select(p => (int?)p.ParentAccountId)
            .FirstOrDefaultAsync(ct);
        if (accountId is null) return new RecipientList("Player guardians", Array.Empty<ResolvedRecipient>());

        var ids = new List<int> { accountId.Value };
        var rows = await _db.ParentAccounts
            .Where(p => p.Id == accountId
                && !p.NoCommunications
                && ((p.CellPhone != null && p.CellPhone != "")
                    || (p.User != null && p.User.Email != null && p.User.Email != "")))
            .Select(p => new { p.Id, p.CellPhone, p.FirstName, p.LastName, p.Language, p.HasWhatsApp, Email = p.User!.Email })
            .ToListAsync(ct);
        var parents = rows
            .Select(r => new ResolvedRecipient(
                r.CellPhone ?? string.Empty, $"{r.FirstName} {r.LastName}".Trim(), r.Id, r.Language, r.Email, r.HasWhatsApp))
            .ToList();
        var contacts = await LoadContactsAsync(ids, ct);
        return new RecipientList("Player guardians", DedupeByReachability(parents.Concat(contacts)));
    }

    /// <summary>Additional parent/guardian contacts as recipients. <paramref name="parentAccountIds"/>
    /// null = all accounts (the "all parents" scope); otherwise restricts to the given accounts.
    /// Only contacts reachable by phone or email are emitted.</summary>
    private async Task<List<ResolvedRecipient>> LoadContactsAsync(IReadOnlyCollection<int>? parentAccountIds, CancellationToken ct)
    {
        if (parentAccountIds is { Count: 0 }) return new List<ResolvedRecipient>();

        // Contacts inherit the family's no-communications opt-out — if the primary parent's
        // ParentAccount is flagged, every additional guardian on that family is filtered out too.
        var q = _db.ParentContacts
            .Where(c => !c.ParentAccount!.NoCommunications
                && ((c.CellPhone != null && c.CellPhone != "") || (c.Email != null && c.Email != "")));
        if (parentAccountIds is not null)
            q = q.Where(c => parentAccountIds.Contains(c.ParentAccountId));

        var rows = await q
            .Select(c => new { c.ParentAccountId, c.CellPhone, c.FirstName, c.LastName, c.Language, c.HasWhatsApp, c.Email })
            .ToListAsync(ct);
        return rows
            .Select(c => new ResolvedRecipient(
                c.CellPhone ?? string.Empty, $"{c.FirstName} {c.LastName}".Trim(), c.ParentAccountId, c.Language, c.Email, c.HasWhatsApp))
            .ToList();
    }

    /// <summary>Keep the first recipient per reachable identity (normalized phone, else lowercased
    /// email) so a guardian who shares a number/email with the primary parent isn't double-sent.
    /// Callers list primary parents before contacts so the primary wins a tie.</summary>
    private static List<ResolvedRecipient> DedupeByReachability(IEnumerable<ResolvedRecipient> recipients)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var result = new List<ResolvedRecipient>();
        foreach (var r in recipients)
        {
            var key = !string.IsNullOrWhiteSpace(r.Phone) ? r.Phone.Trim()
                : !string.IsNullOrWhiteSpace(r.Email) ? r.Email!.Trim().ToLowerInvariant()
                : null;
            if (key is null || seen.Add(key)) result.Add(r);
        }
        return result;
    }

    /// <summary>One-shot lookup for a single typed phone. Uses <see cref="PhoneNormalizer.Variants"/>
    /// so the match works whether the admin pasted the E.164 form, the bare 10 digits, or anything
    /// in between.</summary>
    private async Task<bool?> LookupHasWhatsAppAsync(string phone, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(phone)) return null;
        var candidates = PhoneNormalizer.Variants(phone);
        var match = await _db.ParentAccounts
            .Where(p => p.CellPhone != null && candidates.Contains(p.CellPhone))
            .Select(p => (bool?)p.HasWhatsApp)
            .FirstOrDefaultAsync(ct);
        return match;
    }

    private static bool? ResolveHasWhatsAppFromLookup(string phone, IReadOnlyDictionary<string, bool> lookup)
    {
        if (string.IsNullOrWhiteSpace(phone)) return null;
        foreach (var v in PhoneNormalizer.Variants(phone))
            if (lookup.TryGetValue(v, out var has)) return has;
        return null;
    }
}
