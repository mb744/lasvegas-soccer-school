using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using QuestPDF.Infrastructure;
using SoccerSchool.Api;
using SoccerSchool.Api.Data;
using SoccerSchool.Api.Domain;
using SoccerSchool.Api.Options;
using SoccerSchool.Api.Services;

QuestPDF.Settings.License = LicenseType.Community;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<AppOptions>(builder.Configuration.GetSection(AppOptions.SectionName));
builder.Services.Configure<AcsOptions>(builder.Configuration.GetSection(AcsOptions.SectionName));

builder.Services.AddDbContext<AppDbContext>(opts =>
{
    var cs = builder.Configuration.GetConnectionString("DefaultConnection")
        ?? throw new InvalidOperationException("ConnectionStrings:DefaultConnection is not configured.");

    // Azure SQL via managed identity: connection string includes "Authentication=Active Directory Default".
    // EF Core / Microsoft.Data.SqlClient handles the token acquisition through DefaultAzureCredential,
    // which picks up the Container App's user-assigned managed identity (AZURE_CLIENT_ID env var).
    opts.UseSqlServer(cs);
});

builder.Services.AddIdentity<ApplicationUser, IdentityRole>(opts =>
{
    opts.User.RequireUniqueEmail = true;
    opts.Password.RequiredLength = 8;
    opts.Password.RequireNonAlphanumeric = false;
    opts.Password.RequireUppercase = false;
    opts.SignIn.RequireConfirmedAccount = false;
})
.AddEntityFrameworkStores<AppDbContext>()
.AddDefaultTokenProviders();

builder.Services.ConfigureApplicationCookie(opts =>
{
    opts.Cookie.Name = "lvss.auth";
    opts.Cookie.HttpOnly = true;
    opts.Cookie.SameSite = SameSiteMode.Lax;
    opts.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
    opts.ExpireTimeSpan = TimeSpan.FromDays(30);
    opts.SlidingExpiration = true;

    // API requests get JSON-friendly status codes instead of redirects.
    opts.Events.OnRedirectToLogin = ctx =>
    {
        if (ctx.Request.Path.StartsWithSegments("/api"))
        {
            ctx.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return Task.CompletedTask;
        }
        ctx.Response.Redirect(ctx.RedirectUri);
        return Task.CompletedTask;
    };
    opts.Events.OnRedirectToAccessDenied = ctx =>
    {
        if (ctx.Request.Path.StartsWithSegments("/api"))
        {
            ctx.Response.StatusCode = StatusCodes.Status403Forbidden;
            return Task.CompletedTask;
        }
        ctx.Response.Redirect(ctx.RedirectUri);
        return Task.CompletedTask;
    };
});

var oauth = builder.Configuration.GetSection($"{AppOptions.SectionName}:OAuth").Get<AppOptions.OAuthOptions>() ?? new();
var authBuilder = builder.Services.AddAuthentication();
if (oauth.Google.IsConfigured)
{
    authBuilder.AddGoogle(opts =>
    {
        opts.ClientId = oauth.Google.ClientId;
        opts.ClientSecret = oauth.Google.ClientSecret;
        opts.SignInScheme = IdentityConstants.ExternalScheme;
    });
}
if (oauth.Facebook.IsConfigured)
{
    authBuilder.AddFacebook(opts =>
    {
        opts.AppId = oauth.Facebook.AppId;
        opts.AppSecret = oauth.Facebook.AppSecret;
        opts.SignInScheme = IdentityConstants.ExternalScheme;
    });
}

builder.Services.AddAuthorization();

builder.Services.AddScoped<IOutreachSender, OutreachSender>();
builder.Services.AddSingleton<IWaiverPdfGenerator, WaiverPdfGenerator>();

builder.Services.AddControllers();
builder.Services.AddOpenApi();

// CORS: only relevant when running with the Vite dev proxy on a different origin.
// In the deployed single-container topology the React build is served from the same origin.
var allowedOrigins = builder.Configuration
    .GetSection($"{AppOptions.SectionName}:Cors:AllowedOrigins")
    .Get<string[]>() ?? Array.Empty<string>();
builder.Services.AddCors(o => o.AddDefaultPolicy(p => p
    .WithOrigins(allowedOrigins)
    .AllowAnyHeader()
    .AllowAnyMethod()
    .AllowCredentials()));

var app = builder.Build();

// Apply migrations on startup. Safe because EF will skip already-applied ones.
// Wrapped in a retry because Azure SQL serverless may be paused on cold start.
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await MigrateWithRetryAsync(db, app.Logger);
    await SeedAdminAsync(scope.ServiceProvider, app.Logger);
}

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseCors();
app.UseAuthentication();
app.UseAuthorization();

// Serve the React build from wwwroot (single-container deploy).
// In dev this folder is empty; the Vite dev server handles the UI.
app.UseDefaultFiles();
app.UseStaticFiles();

app.MapControllers();
app.MapGet("/health", () => Results.Ok(new { status = "ok", time = DateTime.UtcNow }));

// SPA fallback: any non-API request that isn't a static file returns index.html
// so React Router can handle client-side routes (/login, /signup, /register, /admin).
app.MapFallbackToFile("index.html");

app.Run();

static async Task MigrateWithRetryAsync(AppDbContext db, ILogger logger)
{
    for (var attempt = 1; attempt <= 6; attempt++)
    {
        try
        {
            await db.Database.MigrateAsync();
            return;
        }
        catch (SqlException ex) when (attempt < 6)
        {
            var delay = TimeSpan.FromSeconds(Math.Pow(2, attempt));
            logger.LogWarning(ex, "Migration attempt {Attempt} failed, retrying in {Delay}s.", attempt, delay.TotalSeconds);
            await Task.Delay(delay);
        }
    }
}

static async Task SeedAdminAsync(IServiceProvider services, ILogger logger)
{
    var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();
    if (!await roleManager.RoleExistsAsync(Roles.Admin))
        await roleManager.CreateAsync(new IdentityRole(Roles.Admin));

    var bootstrap = services.GetRequiredService<Microsoft.Extensions.Options.IOptions<AppOptions>>().Value.Admin;
    if (string.IsNullOrWhiteSpace(bootstrap.Email))
        return;

    var users = services.GetRequiredService<UserManager<ApplicationUser>>();
    var existing = await users.FindByEmailAsync(bootstrap.Email);
    if (existing is null)
    {
        if (string.IsNullOrWhiteSpace(bootstrap.Password))
        {
            logger.LogWarning("Admin bootstrap email {Email} configured but no password set; skipping create.", bootstrap.Email);
            return;
        }
        existing = new ApplicationUser
        {
            UserName = bootstrap.Email,
            Email = bootstrap.Email,
            EmailConfirmed = true
        };
        var create = await users.CreateAsync(existing, bootstrap.Password);
        if (!create.Succeeded)
        {
            logger.LogError("Failed to create admin bootstrap user: {Errors}",
                string.Join(", ", create.Errors.Select(e => e.Description)));
            return;
        }
        logger.LogInformation("Created admin bootstrap user {Email}.", bootstrap.Email);
    }
    if (!await users.IsInRoleAsync(existing, Roles.Admin))
    {
        await users.AddToRoleAsync(existing, Roles.Admin);
        logger.LogInformation("Granted Admin role to {Email}.", existing.Email);
    }
}
