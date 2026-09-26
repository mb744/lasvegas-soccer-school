using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using SoccerSchool.Api.Data;
using SoccerSchool.Api.Domain;
using SoccerSchool.Api.Services;

namespace SoccerSchool.Api.Tests;

/// <summary>Real PermissionService + Identity over an in-memory database, one per test.</summary>
public sealed class Harness : IAsyncDisposable
{
    private readonly ServiceProvider _root;
    private readonly AsyncServiceScope _scope;

    public Harness()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddMemoryCache();
        var dbName = Guid.NewGuid().ToString();
        services.AddDbContext<AppDbContext>(o => o.UseInMemoryDatabase(dbName));
        services.AddIdentityCore<ApplicationUser>()
            .AddRoles<IdentityRole>()
            .AddEntityFrameworkStores<AppDbContext>();
        services.AddScoped<ICoachScopeService, CoachScopeService>();
        services.AddScoped<IPermissionService, PermissionService>();
        _root = services.BuildServiceProvider();
        _scope = _root.CreateAsyncScope();
    }

    public IServiceProvider Services => _scope.ServiceProvider;
    public AppDbContext Db => Services.GetRequiredService<AppDbContext>();
    public UserManager<ApplicationUser> Users => Services.GetRequiredService<UserManager<ApplicationUser>>();
    public IPermissionService Permissions => Services.GetRequiredService<IPermissionService>();

    public async Task<ApplicationUser> UserAsync(string email, bool admin = false, bool confirmed = true)
    {
        var user = new ApplicationUser { UserName = email, Email = email, EmailConfirmed = confirmed };
        Assert.True((await Users.CreateAsync(user)).Succeeded);
        if (admin)
        {
            var roles = Services.GetRequiredService<RoleManager<IdentityRole>>();
            if (!await roles.RoleExistsAsync(Roles.Admin)) await roles.CreateAsync(new IdentityRole(Roles.Admin));
            await Users.AddToRoleAsync(user, Roles.Admin);
        }
        return user;
    }

    /// <summary>Links the user to a coach card on a new team (what makes them a coach).</summary>
    public async Task<int> CoachAsync(ApplicationUser user)
    {
        var team = new Team { Name = $"Team {Guid.NewGuid():N}" };
        Db.Teams.Add(team);
        await Db.SaveChangesAsync();
        Db.TeamCoaches.Add(new TeamCoach { TeamId = team.Id, Name = "Coach", Email = user.Email, UserId = user.Id });
        await Db.SaveChangesAsync();
        return team.Id;
    }

    public static ClaimsPrincipal Principal(ApplicationUser user) =>
        new(new ClaimsIdentity(new[] { new Claim(ClaimTypes.NameIdentifier, user.Id) }, "test"));

    public async ValueTask DisposeAsync()
    {
        await _scope.DisposeAsync();
        await _root.DisposeAsync();
    }
}

public class PermissionCatalogTests
{
    [Fact]
    public void Keys_are_unique_and_well_formed()
    {
        var keys = Permissions.All.Select(p => p.Key).ToList();
        Assert.Equal(keys.Count, keys.Distinct().Count());
        Assert.All(keys, k => Assert.Matches("^[a-z]+\\.[a-z]+$", k));
        Assert.All(Permissions.All, p => Assert.DoesNotContain(AccessRole.Admin, p.DefaultRoles));
    }

    [Fact]
    public void Creation_permissions_are_grantable_and_off_for_coaches_by_default()
    {
        foreach (var key in new[] { Permissions.DrillsCreate, Permissions.EventsCreate })
        {
            var info = Permissions.Find(key)!;
            Assert.True(info.Grantable);
            Assert.Empty(info.DefaultRoles);
        }
    }

    [Fact]
    public void RequirePermission_rejects_unknown_keys() =>
        Assert.Throws<ArgumentException>(() => new RequirePermissionAttribute("drills.fly"));

    [Fact]
    public async Task Policy_provider_builds_any_of_policies_and_falls_back()
    {
        var provider = new PermissionPolicyProvider(Microsoft.Extensions.Options.Options.Create(new AuthorizationOptions()));
        var policy = await provider.GetPolicyAsync("perm:roles.manage|users.manage");
        var req = Assert.Single(policy!.Requirements.OfType<PermissionRequirement>());
        Assert.Equal(new[] { "roles.manage", "users.manage" }, req.AnyOf);
        Assert.Null(await provider.GetPolicyAsync("SomeOtherPolicy"));
    }
}

public class PermissionServiceTests
{
    [Fact]
    public async Task Seeding_applies_defaults_once_and_is_idempotent()
    {
        await using var h = new Harness();
        await h.Permissions.EnsureSeededAsync(default);
        await h.Permissions.EnsureSeededAsync(default);

        var matrix = await h.Permissions.GetRoleMatrixAsync(default);
        Assert.Equal(
            new[] { "attendance.view", "drills.assign", "drills.view", "events.view", "media.moderate", "players.view", "teams.view" },
            matrix[AccessRole.Coach].OrderBy(k => k));
        Assert.Equal(new[] { "events.view", "family.manage" }, matrix[AccessRole.Parent].OrderBy(k => k));
        Assert.Equal(Permissions.All.Count, await h.Db.KnownPermissions.CountAsync());
        Assert.Equal(9, await h.Db.RolePermissions.CountAsync()); // no duplicates from the second run
    }

    [Fact]
    public async Task A_default_turned_off_by_an_admin_stays_off_after_reseeding()
    {
        await using var h = new Harness();
        var admin = await h.UserAsync("admin@test", admin: true);
        await h.Permissions.EnsureSeededAsync(default);

        Assert.Null(await h.Permissions.SetRolePermissionAsync(AccessRole.Coach, Permissions.MediaModerate, false, Harness.Principal(admin), default));
        await h.Permissions.EnsureSeededAsync(default);

        Assert.DoesNotContain(Permissions.MediaModerate, (await h.Permissions.GetRoleMatrixAsync(default))[AccessRole.Coach]);
    }

    [Fact]
    public async Task Admin_has_every_permission()
    {
        await using var h = new Harness();
        await h.Permissions.EnsureSeededAsync(default);
        var admin = await h.UserAsync("admin@test", admin: true);

        var eff = await h.Permissions.GetForUserAsync(admin, default);
        Assert.True(eff.IsAdmin);
        Assert.All(Permissions.All, p => Assert.True(eff.Has(p.Key), p.Key));
    }

    [Fact]
    public async Task Plain_parent_gets_only_parent_permissions()
    {
        await using var h = new Harness();
        await h.Permissions.EnsureSeededAsync(default);
        var parent = await h.UserAsync("parent@test");

        var eff = await h.Permissions.GetForUserAsync(parent, default);
        Assert.False(eff.IsAdmin);
        Assert.False(eff.IsCoach);
        Assert.Equal(new[] { AccessRole.Parent }, eff.Roles);
        Assert.Equal(new[] { "events.view", "family.manage" }, eff.Keys.OrderBy(k => k));
    }

    [Fact]
    public async Task Coach_gets_coach_and_parent_defaults_but_cannot_create()
    {
        await using var h = new Harness();
        await h.Permissions.EnsureSeededAsync(default);
        var coach = await h.UserAsync("coach@test");
        var teamId = await h.CoachAsync(coach);

        var eff = await h.Permissions.GetForUserAsync(coach, default);
        Assert.Equal(new[] { teamId }, eff.CoachTeamIds);
        Assert.Contains(AccessRole.Coach, eff.Roles);
        Assert.True(eff.Has(Permissions.DrillsAssign));
        Assert.True(eff.Has(Permissions.FamilyManage));
        Assert.False(eff.Has(Permissions.DrillsCreate));
        Assert.False(eff.Has(Permissions.EventsCreate));
        Assert.False(eff.Has(Permissions.AdminAccess));
    }

    [Fact]
    public async Task Grant_and_revoke_take_effect_immediately_even_when_cached()
    {
        await using var h = new Harness();
        await h.Permissions.EnsureSeededAsync(default);
        var admin = await h.UserAsync("admin@test", admin: true);
        var coach = await h.UserAsync("coach@test");
        await h.CoachAsync(coach);
        var coachPrincipal = Harness.Principal(coach);

        Assert.False((await h.Permissions.GetAsync(coachPrincipal, default))!.Has(Permissions.DrillsCreate)); // now cached

        Assert.Null(await h.Permissions.SetUserGrantAsync(coach.Id, Permissions.DrillsCreate, true, Harness.Principal(admin), default));
        Assert.True((await h.Permissions.GetAsync(coachPrincipal, default))!.Has(Permissions.DrillsCreate));

        Assert.Null(await h.Permissions.SetUserGrantAsync(coach.Id, Permissions.DrillsCreate, false, Harness.Principal(admin), default));
        Assert.False((await h.Permissions.GetAsync(coachPrincipal, default))!.Has(Permissions.DrillsCreate));
    }

    [Fact]
    public async Task Invalid_changes_are_rejected()
    {
        await using var h = new Harness();
        await h.Permissions.EnsureSeededAsync(default);
        var admin = await h.UserAsync("admin@test", admin: true);
        var parent = await h.UserAsync("parent@test");
        var actor = Harness.Principal(admin);

        Assert.NotNull(await h.Permissions.SetRolePermissionAsync(AccessRole.Admin, Permissions.DrillsView, false, actor, default));
        Assert.NotNull(await h.Permissions.SetRolePermissionAsync(AccessRole.Coach, "drills.fly", true, actor, default));
        // Only grantable permissions can be given to individuals.
        Assert.NotNull(await h.Permissions.SetUserGrantAsync(parent.Id, Permissions.UsersManage, true, actor, default));
        Assert.Equal("User not found.", await h.Permissions.SetUserGrantAsync("nobody", Permissions.DrillsCreate, true, actor, default));
        Assert.False((await h.Permissions.GetForUserAsync(parent, default)).Has(Permissions.UsersManage));
    }

    [Fact]
    public async Task Changes_are_audited_with_actor_and_target()
    {
        await using var h = new Harness();
        await h.Permissions.EnsureSeededAsync(default);
        var admin = await h.UserAsync("admin@test", admin: true);
        var coach = await h.UserAsync("coach@test");
        var actor = Harness.Principal(admin);

        await h.Permissions.SetUserGrantAsync(coach.Id, Permissions.EventsCreate, true, actor, default);
        await h.Permissions.SetRolePermissionAsync(AccessRole.Parent, Permissions.EventsView, false, actor, default);
        await h.Permissions.AuditAdminRoleChangeAsync(coach, granted: true, actor, default);
        // A no-op change writes nothing.
        await h.Permissions.SetUserGrantAsync(coach.Id, Permissions.EventsCreate, true, actor, default);

        var log = await h.Db.PermissionAuditEntries.OrderBy(e => e.Id).ToListAsync();
        Assert.Equal(
            new[] { PermissionAuditAction.UserGranted, PermissionAuditAction.RoleRevoked, PermissionAuditAction.AdminRoleGranted },
            log.Select(e => e.Action));
        Assert.All(log, e => Assert.Equal("admin@test", e.ActorEmail));
        Assert.Equal("coach@test", log[0].TargetUserEmail);
        Assert.Equal(AccessRole.Parent, log[1].Role);
    }

    [Fact]
    public async Task Kids_training_tokens_and_anonymous_callers_get_no_permissions()
    {
        await using var h = new Harness();
        await h.Permissions.EnsureSeededAsync(default);
        var kidToken = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim("plid", "7") }, "PlayerJwt"));

        Assert.Null(await h.Permissions.GetAsync(kidToken, default));
        Assert.Null(await h.Permissions.GetAsync(new ClaimsPrincipal(new ClaimsIdentity()), default));
    }
}
