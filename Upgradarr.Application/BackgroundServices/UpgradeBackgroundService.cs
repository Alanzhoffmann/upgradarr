using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Upgradarr.Domain.Interfaces;

namespace Upgradarr.Application.BackgroundServices;

public class UpgradeBackgroundService : BackgroundService
{
    private readonly ILogger<UpgradeBackgroundService> _logger;
    private readonly IServiceProvider _serviceProvider;

    public UpgradeBackgroundService(ILogger<UpgradeBackgroundService> logger, IServiceProvider serviceProvider)
    {
        _logger = logger;
        _serviceProvider = serviceProvider;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using (_logger.BeginScope("UpgradeBackgroundService"))
        {
            _logger.LogStartingUpgradeService();
            stoppingToken.Register(_logger.LogStoppingUpgradeService);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    using var scope = _serviceProvider.CreateScope();
                    var upgradeService = scope.ServiceProvider.GetRequiredService<IUpgradeService>();
                    await upgradeService.ProcessUpgradeAsync(stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogErrorInUpgradeBackgroundService(ex);
                }

                // Wait 10 minutes before the next run
                await Task.Delay(TimeSpan.FromMinutes(10), stoppingToken);
            }
        }
    }
}

internal static partial class UpgradeLoggerExtensions
{
    [LoggerMessage(EventId = 1010, Level = LogLevel.Information, Message = "Starting UpgradeBackgroundService")]
    public static partial void LogStartingUpgradeService(this ILogger logger);

    [LoggerMessage(EventId = 1011, Level = LogLevel.Information, Message = "Stopping UpgradeBackgroundService")]
    public static partial void LogStoppingUpgradeService(this ILogger logger);

    [LoggerMessage(EventId = 4011, Level = LogLevel.Error, Message = "Error in upgrade background service")]
    public static partial void LogErrorInUpgradeBackgroundService(this ILogger logger, Exception ex);
}
