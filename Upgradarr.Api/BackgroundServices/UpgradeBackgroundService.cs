using Upgradarr.Application.Extensions;
using Upgradarr.Domain.Interfaces;

namespace Upgradarr.Api.BackgroundServices;

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
                    await upgradeService.InitializeUpgradeStatesAsync(stoppingToken);
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
