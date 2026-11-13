namespace Infrastructure.BackgroundJobs;

using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

public sealed class RevokedTokenCleanupService(
    IServiceScopeFactory scopeFactory,
    ILogger<RevokedTokenCleanupService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromHours(6));

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            using var scope = scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            var deleted = await db.RevokedTokens
                .Where(t => t.ExpiresAtUtc < DateTime.UtcNow)
                .ExecuteDeleteAsync(stoppingToken);

            if (deleted > 0)
            {
                logger.LogInformation(
                    "Cleaned up {Count} expired revoked tokens.", deleted);
            }
        }
    }
}