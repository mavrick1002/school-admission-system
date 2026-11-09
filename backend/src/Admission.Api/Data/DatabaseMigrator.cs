using Microsoft.EntityFrameworkCore;

namespace Admission.Api.Data;

public static class DatabaseMigrator
{
    private const long MigrationLockKey = 7_203_114_521L;

    public static async Task MigrateAsync(IServiceProvider services, CancellationToken cancellationToken = default)
    {
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AdmissionDbContext>();
        var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger(nameof(DatabaseMigrator));

        // Waiting on the lock must outlast the default 30 second command timeout.
        db.Database.SetCommandTimeout(TimeSpan.FromMinutes(5));

        // An advisory lock belongs to one session, so the connection stays open
        // until it is released and the migrations run on that same connection.
        await db.Database.OpenConnectionAsync(cancellationToken);
        try
        {
            logger.LogInformation("Waiting for the migration lock");
            await db.Database.ExecuteSqlAsync($"SELECT pg_advisory_lock({MigrationLockKey})", cancellationToken);
            try
            {
                await db.Database.MigrateAsync(cancellationToken);
                logger.LogInformation("Database migrations are up to date");
            }
            finally
            {
                await db.Database.ExecuteSqlAsync($"SELECT pg_advisory_unlock({MigrationLockKey})", CancellationToken.None);
            }
        }
        finally
        {
            await db.Database.CloseConnectionAsync();
        }
    }
}
