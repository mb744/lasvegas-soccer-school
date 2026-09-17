using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SoccerSchool.Api.Data;
using SoccerSchool.Api.Options;

namespace SoccerSchool.Api.Services;

/// <summary>
/// Background job that finalizes account deletions once the mobile grace window has elapsed.
/// Wakes hourly, finds every <see cref="Domain.ApplicationUser"/> whose
/// <c>PendingDeletionAt</c> is in the past, and runs <see cref="IAccountDeletionService.PurgeAsync"/>
/// on each. Users can still sign back in and cancel deletion (POST /api/mobile/auth/cancel-deletion)
/// any time before this job catches them.
/// </summary>
public class AccountPurgeJob : BackgroundService
{
    private readonly IServiceProvider _services;
    private readonly ILogger<AccountPurgeJob> _logger;

    private static readonly TimeSpan LoopInterval = TimeSpan.FromHours(1);

    public AccountPurgeJob(IServiceProvider services, ILogger<AccountPurgeJob> logger)
    {
        _services = services;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Stagger the first run so startup migrations/seeds finish first.
        try { await Task.Delay(TimeSpan.FromMinutes(3), stoppingToken); }
        catch (OperationCanceledException) { return; }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RunCycleAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Account purge cycle failed; will retry next interval.");
            }
            try { await Task.Delay(LoopInterval, stoppingToken); }
            catch (OperationCanceledException) { break; }
        }
    }

    private async Task RunCycleAsync(CancellationToken ct)
    {
        using var scope = _services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var deletion = scope.ServiceProvider.GetRequiredService<IAccountDeletionService>();

        var now = DateTime.UtcNow;
        var due = await db.Users
            .Where(u => u.PendingDeletionAt != null && u.PendingDeletionAt <= now)
            .Select(u => u.Id)
            .ToListAsync(ct);

        foreach (var userId in due)
        {
            if (ct.IsCancellationRequested) break;
            await deletion.PurgeAsync(userId, ct);
        }

        if (due.Count > 0)
            _logger.LogInformation("Account purge job finalized {Count} account deletion(s).", due.Count);
    }
}
