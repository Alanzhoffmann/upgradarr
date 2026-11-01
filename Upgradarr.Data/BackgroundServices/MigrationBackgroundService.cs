using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Upgradarr.Data.Interfaces;

namespace Upgradarr.Data.BackgroundServices;

public partial class MigrationBackgroundService(IServiceProvider serviceProvider, ILogger<MigrationBackgroundService> logger, IMigrationState migrationState) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            LogWaitingForMigrations();
            try
            {
                if (!Directory.Exists("/config"))
                {
                    Directory.CreateDirectory("/config");
                }
            }
            catch (Exception)
            {
                // Ignored, let EF fail if inaccessible
            }

            using var scope = serviceProvider.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            await dbContext.Database.MigrateAsync(stoppingToken);
            LogMigrationsCompleted();
            migrationState.IsDone = true;
        }
        catch (Exception ex)
        {
            LogMigrationError(ex);
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Waiting for database migrations to complete...")]
    public partial void LogWaitingForMigrations();

    [LoggerMessage(Level = LogLevel.Information, Message = "Database migrations completed successfully.")]
    public partial void LogMigrationsCompleted();

    [LoggerMessage(Level = LogLevel.Critical, Message = "Failed to apply database migrations.")]
    public partial void LogMigrationError(Exception ex);
}
