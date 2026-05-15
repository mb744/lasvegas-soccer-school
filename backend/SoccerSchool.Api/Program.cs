using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.HttpOverrides;
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
builder.Services.Configure<TwilioOptions>(builder.Configuration.GetSection(TwilioOptions.SectionName));

// Container Apps ingress terminates TLS and forwards HTTP to port 8080. Without this,
// Request.Scheme is "http" and the OAuth handlers send `redirect_uri=http://...` to
// Google/Facebook, which Facebook rejects with "isn't using a secure connection".
// Honor X-Forwarded-Proto/X-Forwarded-For so Request.Scheme reflects the original https.
builder.Services.Configure<ForwardedHeadersOptions>(opts =>
{
    opts.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto | ForwardedHeaders.XForwardedHost;
    // Container Apps ingress comes from a wide range of internal IPs we don't control;
    // trust the headers regardless of source.
    opts.KnownIPNetworks.Clear();
    opts.KnownProxies.Clear();
});

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
builder.Services.AddScoped<IMessageSender, MessageSender>();
builder.Services.AddScoped<IRecipientResolver, RecipientResolver>();
builder.Services.AddScoped<IConversationService, ConversationService>();
builder.Services.AddScoped<IScheduleSyncService, ScheduleSyncService>();
builder.Services.AddScoped<IPhraseTranslator, PhraseTranslator>();
builder.Services.AddHttpClient();
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
    await SeedWhatsAppTemplatesAsync(db, app.Logger);
}

// Must run before UseAuthentication so the Google/Facebook handlers see Request.Scheme=https
// when the Container Apps ingress forwards a TLS-terminated request as plain HTTP.
app.UseForwardedHeaders();

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

// Ensures the canonical WhatsApp Content template exists. Idempotent on ContentSid: only
// inserts when the SID is missing, so admin edits to labels / preview made via the UI
// survive subsequent boots. If you ever cycle the template in Twilio with a new SID, remove
// the old row through the UI first or it will accumulate orphans.
static async Task SeedWhatsAppTemplatesAsync(AppDbContext db, ILogger logger)
{
    const string contentSid = "HX75106c2d166e9b0e87dbb8ecdc325116";
    const string oldPreview = "{{1}} on {{2}} at {{3}}. Wear: {{4}}.";
    const string newPreview = "{{What}} on {{When}} at {{Where}}. Wear: {{wear}}.";

    var existing = await db.WhatsAppTemplates.FirstOrDefaultAsync(t => t.ContentSid == contentSid);
    if (existing is not null)
    {
        // One-time migration: the original seed used positional preview placeholders before we
        // learned the Twilio template uses named ones. Update only if the admin hasn't touched it.
        if (existing.PreviewText == oldPreview)
        {
            existing.PreviewText = newPreview;
            await db.SaveChangesAsync();
            logger.LogInformation("Updated practice_or_game preview to named placeholders.");
        }
        return;
    }

    db.WhatsAppTemplates.Add(new WhatsAppTemplate
    {
        Name = "practice_or_game",
        ContentSid = contentSid,
        Language = Language.English,
        Description = "Canonical practice/game reminder (replaces practice_today, practice_tomorrow_es, practice_mw).",
        // PreviewText uses the same named placeholders the approved Twilio template body uses
        // ({{What}}, {{When}}, etc.) so the admin's compose preview substitutes correctly when we
        // render it client-side.
        PreviewText = "{{What}} on {{When}} at {{Where}}. Wear: {{wear}}.",
        Variables = new List<WhatsAppTemplateVariable>
        {
            new() { Position = 1, Label = "What",  Example = "Practice" },
            new() { Position = 2, Label = "When",  Example = "Wed 5/20 at 5pm" },
            new() { Position = 3, Label = "Where", Example = "Sunset Park, field 3" },
            new() { Position = 4, Label = "wear",  Example = "white jersey" }
        }
    });
    await db.SaveChangesAsync();
    logger.LogInformation("Seeded WhatsApp template practice_or_game ({Sid}).", contentSid);
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

    // Make sure the admin also has a ParentAccount so /api/auth/me returns a name
    // and the UI doesn't show empty header / register form. Names are placeholders
    // that get overwritten the first time admin submits a registration.
    var db = services.GetRequiredService<AppDbContext>();
    var hasAccount = await db.ParentAccounts.AnyAsync(p => p.UserId == existing.Id);
    if (!hasAccount)
    {
        var emailLocal = (existing.Email ?? "").Split('@').FirstOrDefault() ?? "Admin";
        db.ParentAccounts.Add(new ParentAccount
        {
            UserId = existing.Id,
            FirstName = emailLocal,
            LastName = "",
            Language = Language.English,
        });
        await db.SaveChangesAsync();
        logger.LogInformation("Created ParentAccount for admin {Email}.", existing.Email);
    }
}
