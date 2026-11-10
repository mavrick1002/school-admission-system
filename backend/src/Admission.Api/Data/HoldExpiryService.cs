using Microsoft.EntityFrameworkCore;

namespace Admission.Api.Data;

/// <summary>
/// Every 5 seconds, marks HELD holds past expires_at as EXPIRED, so a seat comes back
/// without anyone calling the API.
/// </summary>
/// <remarks>
/// Safe with several backend processes: FOR UPDATE SKIP LOCKED makes each sweep take only
/// rows nobody else has locked. A row being confirmed or cancelled right now is skipped
/// (that request decides its fate), two sweepers split the rows instead of fighting over
/// them, and since the sweeper never waits on a lock it can never be part of a deadlock.
/// </remarks>
public sealed class HoldExpiryService(IServiceScopeFactory scopeFactory, ILogger<HoldExpiryService> logger)
    : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromSeconds(5);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(Interval);
        try
        {
            do
            {
                await SweepAsync(stoppingToken);
            }
            while (await timer.WaitForNextTickAsync(stoppingToken));
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Shutting down.
        }
    }

    private async Task SweepAsync(CancellationToken stoppingToken)
    {
        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<AdmissionDbContext>();

            var expired = await db.Database.ExecuteSqlAsync(
                $"""
                UPDATE holds SET status = 'EXPIRED'
                WHERE status = 'HELD'
                  AND id IN (SELECT id FROM holds
                             WHERE status = 'HELD' AND expires_at <= clock_timestamp()
                             FOR UPDATE SKIP LOCKED)
                """,
                stoppingToken);

            if (expired > 0)
            {
                logger.LogInformation("Marked {Count} expired hold(s) as EXPIRED.", expired);
            }
        }
        catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
        {
            // A database outage must not stop the service; the next tick tries again.
            logger.LogWarning(ex, "Hold expiry sweep failed; retrying in {Seconds} s.", Interval.TotalSeconds);
        }
    }
}
