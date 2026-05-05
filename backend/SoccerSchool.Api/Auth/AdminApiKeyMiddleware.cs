using Microsoft.Extensions.Options;
using SoccerSchool.Api.Options;

namespace SoccerSchool.Api.Auth;

/// <summary>
/// Marker attribute on controllers/actions that require the admin API key header.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public class RequireAdminAttribute : Attribute { }

public class AdminApiKeyMiddleware
{
    public const string HeaderName = "X-Admin-Key";
    private readonly RequestDelegate _next;

    public AdminApiKeyMiddleware(RequestDelegate next) => _next = next;

    public async Task Invoke(HttpContext context, IOptions<AppOptions> opts)
    {
        var endpoint = context.GetEndpoint();
        var requiresAdmin = endpoint?.Metadata.GetMetadata<RequireAdminAttribute>() != null;

        if (requiresAdmin)
        {
            var configuredKey = opts.Value.AdminApiKey;
            var providedKey = context.Request.Headers[HeaderName].ToString();

            if (string.IsNullOrEmpty(configuredKey) || providedKey != configuredKey)
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                await context.Response.WriteAsync("Missing or invalid admin API key.");
                return;
            }
        }

        await _next(context);
    }
}
