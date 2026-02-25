using Huntarr.Net.Api.Extensions;
using Huntarr.Net.Api.Services;

namespace Huntarr.Net.Api.BackgroundServices;

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

            // Initial delay to allow application to fully start
            await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);

            // Initialize upgrade states on first run
            using (var scope = _serviceProvider.CreateScope())
            {
                var upgradeService = scope.ServiceProvider.GetRequiredService<UpgradeService>();
                try
                {
                    await upgradeService.InitializeUpgradeStatesAsync(stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogErrorInitializingUpgradeStates(ex);
                }
            }

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    using var scope = _serviceProvider.CreateScope();
                    await ProcessUpgradeAsync(scope.ServiceProvider, stoppingToken);
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

    private static async Task ProcessUpgradeAsync(IServiceProvider serviceProvider, CancellationToken cancellationToken = default)
    {
        var upgradeService = serviceProvider.GetRequiredService<UpgradeService>();
        await upgradeService.ProcessUpgradeAsync(cancellationToken);
    }
}
