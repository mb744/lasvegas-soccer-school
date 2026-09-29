using SoccerSchool.Api.Domain;
using SoccerSchool.Api.Options;
using SoccerSchool.Api.Services;

namespace SoccerSchool.Api.Tests;

public class NoAppParentsGroupTests
{
    private static RecipientResolver Resolver(Harness h) =>
        new(h.Db, Microsoft.Extensions.Options.Options.Create(new AppOptions()));

    private static async Task<ParentAccount> FamilyAsync(Harness h, ApplicationUser owner, string name, bool noComms = false)
    {
        var family = new ParentAccount { UserId = owner.Id, FirstName = name, LastName = "Parent", NoCommunications = noComms };
        h.Db.ParentAccounts.Add(family);
        await h.Db.SaveChangesAsync();
        return family;
    }

    private static async Task<IReadOnlyList<string?>> NoAppEmailsAsync(Harness h)
    {
        var list = await Resolver(h).ResolveAsync(
            new RecipientTarget(RecipientTargetKind.DynamicGroup, DynamicGroupKey: RecipientResolver.DynamicNoAppParents), default);
        return list.Recipients.Select(r => r.Email).ToList();
    }

    [Fact]
    public async Task Parents_with_any_app_signal_are_excluded()
    {
        await using var h = new Harness();
        var never = await h.UserAsync("never@test");
        var checkedIn = await h.UserAsync("checkin@test");
        var pushOnly = await h.UserAsync("push@test");
        var signedIn = await h.UserAsync("signedin@test");
        foreach (var u in new[] { never, checkedIn, pushOnly, signedIn }) await FamilyAsync(h, u, u.Email!);

        h.Db.MobileAppInstalls.Add(new MobileAppInstall { InstallationId = "i1", UserId = checkedIn.Id });
        h.Db.DeviceTokens.Add(new DeviceToken { UserId = pushOnly.Id, ExpoPushToken = "ExponentPushToken[x]" });
        // Revoked/rotated tokens still prove they signed in on mobile once.
        h.Db.MobileRefreshTokens.Add(new MobileRefreshToken { UserId = signedIn.Id, TokenHash = "h", ExpiresAt = DateTime.UtcNow.AddDays(-1), RevokedAt = DateTime.UtcNow });
        await h.Db.SaveChangesAsync();

        Assert.Equal(new[] { "never@test" }, await NoAppEmailsAsync(h));
    }

    [Fact]
    public async Task Co_parent_without_the_app_is_included_even_when_partner_has_it()
    {
        await using var h = new Harness();
        var mom = await h.UserAsync("mom@test");
        var family = await FamilyAsync(h, mom, "Mom");
        h.Db.MobileAppInstalls.Add(new MobileAppInstall { InstallationId = "i1", UserId = mom.Id });

        var dadWithLogin = await h.UserAsync("dad-app@test");
        h.Db.MobileAppInstalls.Add(new MobileAppInstall { InstallationId = "i2", UserId = dadWithLogin.Id });
        h.Db.ParentContacts.AddRange(
            new ParentContact { ParentAccountId = family.Id, FirstName = "Dad", LastName = "NoLogin", Email = "dad-nologin@test" },
            new ParentContact { ParentAccountId = family.Id, FirstName = "Dad", LastName = "App", Email = "dad-app@test", UserId = dadWithLogin.Id },
            // View-only relatives get the app, not the team's emails.
            new ParentContact { ParentAccountId = family.Id, FirstName = "Grandma", LastName = "Viewer", Email = "grandma@test", AccessLevel = FamilyAccessLevel.Viewer });
        await h.Db.SaveChangesAsync();

        Assert.Equal(new[] { "dad-nologin@test" }, await NoAppEmailsAsync(h));
    }

    [Fact]
    public async Task Opted_out_families_are_excluded_and_group_is_listed()
    {
        await using var h = new Harness();
        await FamilyAsync(h, await h.UserAsync("optout@test"), "OptOut", noComms: true);
        await FamilyAsync(h, await h.UserAsync("in@test"), "In");

        Assert.Equal(new[] { "in@test" }, await NoAppEmailsAsync(h));

        var groups = await Resolver(h).ListDynamicGroupsAsync(default);
        var group = Assert.Single(groups, g => g.Key == RecipientResolver.DynamicNoAppParents);
        Assert.Equal(1, group.Count);
    }
}
