using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;
using SoccerSchool.Api.Services;

namespace SoccerSchool.Api;

/// <summary>
/// Requires the signed-in adult to hold at least one of the given permissions (see
/// <see cref="Permissions"/>). Combine with the controller's <c>AuthenticationSchemes</c> as usual.
/// Checks permissions only — endpoints still enforce scope (a coach's own teams, a parent's own
/// family) themselves.
/// <code>[RequirePermission(Permissions.DrillsCreate)]</code>
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true, Inherited = true)]
public sealed class RequirePermissionAttribute : AuthorizeAttribute
{
    public const string PolicyPrefix = "perm:";

    public RequirePermissionAttribute(params string[] anyOf)
    {
        if (anyOf.Length == 0) throw new ArgumentException("At least one permission is required.", nameof(anyOf));
        foreach (var p in anyOf)
            if (!Permissions.IsKnown(p)) throw new ArgumentException($"Unknown permission '{p}'.", nameof(anyOf));
        Policy = PolicyPrefix + string.Join('|', anyOf);
    }
}

public sealed class PermissionRequirement : IAuthorizationRequirement
{
    public PermissionRequirement(IReadOnlyList<string> anyOf) => AnyOf = anyOf;
    public IReadOnlyList<string> AnyOf { get; }
}

/// <summary>Builds <c>perm:a|b</c> policies on demand; everything else falls through to the default
/// provider, so existing <c>[Authorize(Roles = ...)]</c> and named policies keep working.</summary>
public sealed class PermissionPolicyProvider : IAuthorizationPolicyProvider
{
    private readonly DefaultAuthorizationPolicyProvider _fallback;

    public PermissionPolicyProvider(IOptions<AuthorizationOptions> options) =>
        _fallback = new DefaultAuthorizationPolicyProvider(options);

    public Task<AuthorizationPolicy?> GetPolicyAsync(string policyName)
    {
        if (!policyName.StartsWith(RequirePermissionAttribute.PolicyPrefix, StringComparison.Ordinal))
            return _fallback.GetPolicyAsync(policyName);

        var anyOf = policyName[RequirePermissionAttribute.PolicyPrefix.Length..]
            .Split('|', StringSplitOptions.RemoveEmptyEntries);
        var policy = new AuthorizationPolicyBuilder()
            .RequireAuthenticatedUser()
            .AddRequirements(new PermissionRequirement(anyOf))
            .Build();
        return Task.FromResult<AuthorizationPolicy?>(policy);
    }

    public Task<AuthorizationPolicy> GetDefaultPolicyAsync() => _fallback.GetDefaultPolicyAsync();
    public Task<AuthorizationPolicy?> GetFallbackPolicyAsync() => _fallback.GetFallbackPolicyAsync();
}

/// <summary>Resolves the caller's effective permissions (cached briefly) and succeeds if they hold
/// any required key. Kids' Daily Training tokens carry no Identity user, so they never pass.</summary>
public sealed class PermissionHandler : AuthorizationHandler<PermissionRequirement>
{
    private readonly IPermissionService _permissions;

    public PermissionHandler(IPermissionService permissions) => _permissions = permissions;

    protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context, PermissionRequirement requirement)
    {
        var effective = await _permissions.GetAsync(context.User, CancellationToken.None);
        if (effective is not null && effective.HasAny(requirement.AnyOf))
            context.Succeed(requirement);
    }
}
