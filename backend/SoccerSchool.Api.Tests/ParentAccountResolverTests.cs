using SoccerSchool.Api.Domain;
using SoccerSchool.Api.Services;

namespace SoccerSchool.Api.Tests;

public class ParentAccountResolverTests
{
    private static async Task<ParentAccount> FamilyAsync(Harness h, ApplicationUser? owner, bool withKid)
    {
        var family = new ParentAccount { UserId = owner?.Id, FirstName = "F", LastName = "Amily" };
        h.Db.ParentAccounts.Add(family);
        await h.Db.SaveChangesAsync();
        if (withKid)
        {
            h.Db.Players.Add(new Player { ParentAccountId = family.Id, FirstName = "Kid", LastName = "Amily", DateOfBirth = new DateOnly(2016, 1, 1) });
            await h.Db.SaveChangesAsync();
        }
        return family;
    }

    private static async Task LinkAsync(Harness h, ApplicationUser user, ParentAccount family, FamilyAccessLevel level)
    {
        h.Db.ParentAccountCollaborators.Add(new ParentAccountCollaborator { ParentAccountId = family.Id, UserId = user.Id, AccessLevel = level });
        await h.Db.SaveChangesAsync();
    }

    private static ParentAccountResolver Resolver(Harness h) => new(h.Db, h.Users);

    [Fact]
    public async Task An_empty_own_family_loses_to_a_linked_family()
    {
        // Someone who signed up on their own (empty family) and was then invited to a real one.
        await using var h = new Harness();
        var grandma = await h.UserAsync("grandma@test");
        await FamilyAsync(h, grandma, withKid: false);
        var real = await FamilyAsync(h, await h.UserAsync("mom@test"), withKid: true);
        await LinkAsync(h, grandma, real, FamilyAccessLevel.Viewer);

        var r = Resolver(h);
        Assert.Equal(real.Id, (await r.ResolveByUserIdAsync(grandma.Id, default))!.Id);
        Assert.Equal(FamilyRole.Viewer, await r.RoleInAsync(grandma.Id, real.Id, default));
        // She can't act for the family she only views: the guardian variant falls back to her own.
        Assert.NotEqual(real.Id, (await r.ResolveGuardianByUserIdAsync(grandma.Id, default))!.Id);
        Assert.DoesNotContain(real.Id, await r.FamilyIdsAsync(grandma.Id, guardianOnly: true, default));
        Assert.Contains(real.Id, await r.FamilyIdsAsync(grandma.Id, guardianOnly: false, default));
    }

    [Fact]
    public async Task An_own_family_with_kids_wins_and_guardian_links_beat_view_only()
    {
        await using var h = new Harness();
        var dad = await h.UserAsync("dad@test");
        var viewOnly = await FamilyAsync(h, await h.UserAsync("a@test"), withKid: true);
        var guardianOf = await FamilyAsync(h, await h.UserAsync("b@test"), withKid: true);
        await LinkAsync(h, dad, viewOnly, FamilyAccessLevel.Viewer);
        await LinkAsync(h, dad, guardianOf, FamilyAccessLevel.Guardian);

        var r = Resolver(h);
        Assert.Equal(guardianOf.Id, (await r.ResolveByUserIdAsync(dad.Id, default))!.Id);
        Assert.Equal(guardianOf.Id, (await r.ResolveGuardianByUserIdAsync(dad.Id, default))!.Id);

        var own = await FamilyAsync(h, dad, withKid: true);
        Assert.Equal(own.Id, (await r.ResolveByUserIdAsync(dad.Id, default))!.Id);
        Assert.Equal(FamilyRole.Owner, await r.RoleInAsync(dad.Id, own.Id, default));
        Assert.Null(await r.RoleInAsync((await h.UserAsync("stranger@test")).Id, own.Id, default));
    }
}
