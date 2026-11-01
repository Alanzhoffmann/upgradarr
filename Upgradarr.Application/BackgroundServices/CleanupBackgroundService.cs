using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Upgradarr.Application.Options;
using Upgradarr.Domain.Interfaces;

namespace Upgradarr.Application.BackgroundServices;

public partial class CleanupBackgroundService(
    ILogger<CleanupBackgroundService> logger,
    IOptionsMonitor<CleanupOptions> optionsMonitor,
    IServiceProvider serviceProvider
) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using (logger.BeginScope("CleanupBackgroundService"))
        {
            LogStartingCleanupBackgroundService();
            stoppingToken.Register(LogStoppingCleanupBackgroundService);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    using var scope = serviceProvider.CreateScope();
                    var cleanupService = scope.ServiceProvider.GetRequiredService<ICleanupService>();
                    await cleanupService.PerformCleanupAsync(stoppingToken);
                }
                catch (Exception ex)
                {
                    LogCleanupError(ex, ex.Message);
                }

                await Task.Delay(TimeSpan.FromMinutes(Math.Max(1, optionsMonitor.CurrentValue.CleanupIntervalMinutes)), stoppingToken);
            }
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Starting Cleanup Background Service.")]
    public partial void LogStartingCleanupBackgroundService();

    [LoggerMessage(Level = LogLevel.Information, Message = "Stopping Cleanup Background Service.")]
    public partial void LogStoppingCleanupBackgroundService();

    [LoggerMessage(Level = LogLevel.Error, Message = "An error occurred during cleanup: {ErrorMessage}")]
    public partial void LogCleanupError(Exception ex, string errorMessage);
}
