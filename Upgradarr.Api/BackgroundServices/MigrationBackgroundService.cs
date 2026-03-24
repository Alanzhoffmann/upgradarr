using Microsoft.EntityFrameworkCore;
using Upgradarr.Data;
using Upgradarr.Data.Interfaces;

namespace Upgradarr.Api.BackgroundServices;

public class MigrationBackgroundService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<MigrationBackgroundService> _logger;

    private readonly IMigrationState _migrationState;

    public MigrationBackgroundService(IServiceProvider serviceProvider, ILogger<MigrationBackgroundService> logger, IMigrationState migrationState)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        _migrationState = migrationState;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            _logger.LogWaitingForMigrations();
            try
            {
                if (!Directory.Exists("/config"))
                {
                    Directory.CreateDirectory("/config");
                }
            }
            catch (Exception)
            { /* Ignored, let EF fail if inaccessible */
            }

            using var scope = _serviceProvider.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            await dbContext.Database.MigrateAsync(stoppingToken);
            _logger.LogMigrationsCompleted();
            _migrationState.IsDone = true;
        }
        catch (Exception ex)
        {
            _logger.LogMigrationError(ex);
        }
    }
}

internal static partial class MigrationLoggerExtensions
{
    [LoggerMessage(EventId = 0, Level = LogLevel.Information, Message = "Waiting for database migrations to complete...")]
    public static partial void LogWaitingForMigrations(this ILogger logger);

    [LoggerMessage(EventId = 1, Level = LogLevel.Information, Message = "Database migrations completed successfully.")]
    public static partial void LogMigrationsCompleted(this ILogger logger);

    [LoggerMessage(EventId = 2, Level = LogLevel.Critical, Message = "Failed to apply database migrations.")]
    public static partial void LogMigrationError(this ILogger logger, Exception ex);
}
