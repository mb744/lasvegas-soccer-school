using SoccerSchool.Api.Domain;
using SoccerSchool.Api.Options;
using SoccerSchool.Api.Services;

namespace SoccerSchool.Api.Tests;

public class ArchivedFamilyTests
{
    [Fact]
    public async Task Deleted_accounts_and_families_with_only_archived_kids_get_no_messages_or_chat()
    {
        await using var h = new Harness();
        async Task<ParentAccount> Family(string key, params bool[] kidsArchived)
        {
            var user = await h.UserAsync($"{key}@test");
            var family = new ParentAccount { UserId = user.Id, FirstName = key, LastName = "Family", CellPhone = $"+1702555{key.Length:0000}{kidsArchived.Length}", HasWhatsApp = true };
            h.Db.ParentAccounts.Add(family);
            await h.Db.SaveChangesAsync();
            foreach (var archived in kidsArchived)
                h.Db.Players.Add(new Player
                {
                    ParentAccountId = family.Id, FirstName = $"{key} kid", LastName = "Family", DateOfBirth = new DateOnly(2016, 1, 1),
                    ArchivedAt = archived ? DateTime.UtcNow : null,
                });
            await h.Db.SaveChangesAsync();
            return family;
        }

        var active = await Family("active", false);
        var mixed = await Family("mixed", true, false);   // one kid still active: the family stays
        var gone = await Family("gone", true);             // every kid archived
        var noKids = await Family("nokids");               // just signed up: active
        var deleted = await Family("deleted", true);
        deleted.ReclaimEmailHash = "hash";
        await h.Db.SaveChangesAsync();

        var archived = await FamilyArchive.ArchivedIdsAsync(h.Db, default);
        Assert.Equal(new[] { gone.Id, deleted.Id }.OrderBy(x => x), archived.OrderBy(x => x));

        // "All parents" (and so every bulk send and its count) leaves them out.
        var resolver = new RecipientResolver(h.Db, Microsoft.Extensions.Options.Options.Create(new AppOptions()));
        var all = await resolver.ResolveAsync(new RecipientTarget(RecipientTargetKind.DynamicGroup, DynamicGroupKey: RecipientResolver.DynamicAllParents), default);
        var families = all.Recipients.Select(r => r.ParentAccountId).ToHashSet();
        Assert.Contains(active.Id, families);
        Assert.Contains(mixed.Id, families);
        Assert.Contains(noKids.Id, families);
        Assert.DoesNotContain(gone.Id, families);
        Assert.DoesNotContain(deleted.Id, families);
        var counted = (await resolver.ListDynamicGroupsAsync(default)).Single(g => g.Key == RecipientResolver.DynamicAllParents);
        Assert.Equal(all.Recipients.Count, counted.Count);

        // Out of the group chats too.
        var group = new ChatGroup { Title = "Club chat" };
        h.Db.ChatGroups.Add(group);
        await h.Db.SaveChangesAsync();
        h.Db.ChatGroupMembers.AddRange(
            new ChatGroupMember { ChatGroupId = group.Id, ParentAccountId = active.Id, DisplayName = "active" },
            new ChatGroupMember { ChatGroupId = group.Id, ParentAccountId = gone.Id, DisplayName = "gone" });
        await h.Db.SaveChangesAsync();
        var chat = new ChatService(h.Db, null!, null!, new ParentAccountResolver(h.Db, h.Users), null!);
        Assert.True(await chat.IsMemberAsync(group.Id, active.UserId, default));
        Assert.False(await chat.IsMemberAsync(group.Id, gone.UserId, default));

        // Unarchiving the kid brings the family back.
        var kid = h.Db.Players.IgnoreQueryFiltersFor(gone.Id);
        await PlayerArchive.UnarchiveAsync(h.Db, new[] { kid }, default);
        await h.Db.SaveChangesAsync();
        Assert.DoesNotContain(gone.Id, await FamilyArchive.ArchivedIdsAsync(h.Db, default));
        Assert.True(await chat.IsMemberAsync(group.Id, gone.UserId, default));

        Assert.True(FamilyArchive.IsDeletedLoginEmail("deleted-abc@removed.lvss.local"));
        Assert.False(FamilyArchive.IsDeletedLoginEmail("mom@test"));
    }
}

internal static class PlayerQueryTestExtensions
{
    /// <summary>The id of a family's (possibly archived) kid.</summary>
    public static int IgnoreQueryFiltersFor(this Microsoft.EntityFrameworkCore.DbSet<Player> players, int familyId) =>
        Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.IgnoreQueryFilters(players)
            .Where(p => p.ParentAccountId == familyId).Select(p => p.Id).Single();
}
