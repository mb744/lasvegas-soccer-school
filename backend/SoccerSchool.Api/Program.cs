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
    // 6h idle timeout. SlidingExpiration resets the clock on each request, so an active
    // user stays signed in indefinitely; if they stop using the app for 6 hours, the next
    // request 401s and they're sent to /login.
    opts.ExpireTimeSpan = TimeSpan.FromHours(6);
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
builder.Services.AddScoped<IEmailSender, EmailSender>();
builder.Services.AddScoped<IRecipientResolver, RecipientResolver>();
builder.Services.AddScoped<IConversationService, ConversationService>();
builder.Services.AddScoped<IScheduleSyncService, ScheduleSyncService>();
builder.Services.AddScoped<ITeamSnapSyncService, TeamSnapSyncService>();
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
    await SeedPhraseTranslationsAsync(db, app.Logger);
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

// Ensures the four canonical WhatsApp Content templates exist (practice/game × en/es). Retires
// any prior single-template SIDs we previously seeded so the templates list doesn't carry stale
// rows. Idempotent: subsequent boots are no-ops once the four current SIDs are present.
static async Task SeedWhatsAppTemplatesAsync(AppDbContext db, ILogger logger)
{
    // SIDs from previous canonical-template iterations. When found, the rows are removed so the
    // Templates tab doesn't show stale entries. (FK on Broadcast.WhatsAppTemplateId is SetNull, so
    // historical broadcasts lose their template link but stay readable.)
    string[] retiredSids =
    {
        "HX75106c2d166e9b0e87dbb8ecdc325116", // practice_or_game           (initial)
        "HXcf898f435a72cb186a959ec2398f585e"  // copy_of_practice_or_game   (second iteration)
    };
    var retired = await db.WhatsAppTemplates
        .Where(t => retiredSids.Contains(t.ContentSid))
        .Include(t => t.Variables)
        .ToListAsync();
    if (retired.Count > 0)
    {
        db.WhatsAppTemplates.RemoveRange(retired);
        await db.SaveChangesAsync();
        logger.LogInformation("Retired {Count} legacy WhatsApp template(s).", retired.Count);
    }

    // Practice template: 2 positional vars — {{1}} = when, {{2}} = location.
    static List<WhatsAppTemplateVariable> PracticeVars() => new()
    {
        new() { Position = 1, Label = "When",  Example = "Wed 5/20 at 5pm" },
        new() { Position = 2, Label = "Where", Example = "Sunset Park, field 3" }
    };
    // Game template: 4 positional vars — {{1}} = opponent, {{2}} = when, {{3}} = location, {{4}} = uniform.
    static List<WhatsAppTemplateVariable> GameVars() => new()
    {
        new() { Position = 1, Label = "Opponent", Example = "PRIME SC B17 White" },
        new() { Position = 2, Label = "When",     Example = "Sat 5/24 at 1:50pm" },
        new() { Position = 3, Label = "Where",    Example = "Kellogg-Zaher, field 3" },
        new() { Position = 4, Label = "Uniform",  Example = "white jersey, blue shorts, blue socks" }
    };

    // Preview text mirrors the approved Twilio template body. Used only for admin display in
    // /admin/messaging — Twilio renders the real message server-side from the approved body.
    const string practiceEnPreview = "Our next practice\non\n{{1}}\nLocation\n{{2}}\nPlease wear the practice uniform,\nThank you";
    const string practiceEsPreview = "Nuestra próxima práctica\nel\n{{1}}\nUbicación\n{{2}}\nPor favor use el uniforme de práctica,\nGracias";
    const string gameEnPreview     = "Our next game against\n{{1}}\non\n{{2}}\nLocation\n{{3}}\nUniform\n{{4}}\nThank you";
    const string gameEsPreview     = "Nuestro próximo partido contra\n{{1}}\nel\n{{2}}\nUbicación\n{{3}}\nUniforme\n{{4}}\nGracias";

    (string Sid, string Name, Language Lang, string Preview, Func<List<WhatsAppTemplateVariable>> Vars)[] canonical =
    {
        ("HXeab1edf99ec2e7c9850a734346854ab7", "practice_english", Language.English, practiceEnPreview, PracticeVars),
        ("HX6a3d50eacb1cb6daf9f06b5cab384aca", "practice_spanish", Language.Spanish, practiceEsPreview, PracticeVars),
        ("HX9132ea74f0774cb4b4a8080620d98b7e", "game_english",     Language.English, gameEnPreview,     GameVars),
        ("HX7cc804d91a3266f5084d32b3ecfcc459", "game_spanish",     Language.Spanish, gameEsPreview,     GameVars)
    };

    int added = 0;
    foreach (var t in canonical)
    {
        if (await db.WhatsAppTemplates.AnyAsync(x => x.ContentSid == t.Sid)) continue;
        db.WhatsAppTemplates.Add(new WhatsAppTemplate
        {
            Name = t.Name,
            ContentSid = t.Sid,
            Language = t.Lang,
            Description = "Canonical " + t.Name.Replace('_', ' ') + " reminder.",
            PreviewText = t.Preview,
            Variables = t.Vars()
        });
        added++;
    }
    if (added > 0)
    {
        await db.SaveChangesAsync();
        logger.LogInformation("Seeded {Count} canonical WhatsApp template(s).", added);
    }
}

// Default phrase-dictionary entries used by the bilingual preview/send fallback when a Spanish
// template doesn't exist for a Spanish recipient. Idempotent on English text (skipped if the
// admin already added an entry for the same English source). Admin can edit/extend via the
// Dictionary tab in /admin/messaging.
static async Task SeedPhraseTranslationsAsync(AppDbContext db, ILogger logger)
{
    (string En, string Es)[] defaults =
    {
        ("Game vs", "Juego vs"),
        ("Practice", "Práctica"),
        ("white jersey, blue shorts, blue socks", "camiseta blanca, shorts y calcetas azules"),
        ("all blue", "todo azul"),
        ("Home", "Local"),
        ("Away", "Visitante"),
    };
    var existing = await db.PhraseTranslations
        .Select(p => p.English)
        .ToListAsync();
    var existingSet = new HashSet<string>(existing, StringComparer.OrdinalIgnoreCase);
    var added = 0;
    foreach (var (en, es) in defaults)
    {
        if (existingSet.Contains(en)) continue;
        db.PhraseTranslations.Add(new PhraseTranslation { English = en, Spanish = es });
        added++;
    }
    if (added > 0)
    {
        await db.SaveChangesAsync();
        logger.LogInformation("Seeded {Count} phrase dictionary entries.", added);
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
