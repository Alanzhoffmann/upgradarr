using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Upgradarr.Domain.Interfaces;

namespace Upgradarr.Application.BackgroundServices;

public partial class UpgradeBackgroundService(ILogger<UpgradeBackgroundService> logger, IServiceProvider serviceProvider) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using (logger.BeginScope("UpgradeBackgroundService"))
        {
            LogStartingUpgradeService();
            stoppingToken.Register(LogStoppingUpgradeService);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    using var scope = serviceProvider.CreateScope();
                    var upgradeService = scope.ServiceProvider.GetRequiredService<IUpgradeService>();
                    await upgradeService.ProcessUpgradeAsync(stoppingToken);
                }
                catch (Exception ex)
                {
                    LogErrorInUpgradeBackgroundService(ex);
                }

                // Wait 20 minutes before the next run
                await Task.Delay(TimeSpan.FromMinutes(20), stoppingToken);
            }
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Starting UpgradeBackgroundService")]
    public partial void LogStartingUpgradeService();

    [LoggerMessage(Level = LogLevel.Information, Message = "Stopping UpgradeBackgroundService")]
    public partial void LogStoppingUpgradeService();

    [LoggerMessage(Level = LogLevel.Error, Message = "Error in upgrade background service")]
    public partial void LogErrorInUpgradeBackgroundService(Exception ex);
}
