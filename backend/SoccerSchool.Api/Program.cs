using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using QuestPDF.Infrastructure;
using SoccerSchool.Api.Auth;
using SoccerSchool.Api.Data;
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

builder.Services.AddScoped<IInviteSender, InviteSender>();
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
    .AllowAnyMethod()));

var app = builder.Build();

// Apply migrations on startup. Safe because EF will skip already-applied ones.
// Wrapped in a retry because Azure SQL serverless may be paused on cold start.
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await MigrateWithRetryAsync(db, app.Logger);
}

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseCors();
app.UseMiddleware<AdminApiKeyMiddleware>();
app.UseAuthorization();

// Serve the React build from wwwroot (single-container deploy).
// In dev this folder is empty; the Vite dev server handles the UI.
app.UseDefaultFiles();
app.UseStaticFiles();

app.MapControllers();
app.MapGet("/health", () => Results.Ok(new { status = "ok", time = DateTime.UtcNow }));

// SPA fallback: any non-API request that isn't a static file returns index.html
// so React Router can handle client-side routes (/register/:token, /admin, etc).
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
