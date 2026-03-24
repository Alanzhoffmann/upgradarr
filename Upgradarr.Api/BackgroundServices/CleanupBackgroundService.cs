using Microsoft.Extensions.Options;
using Upgradarr.Application.Options;
using Upgradarr.Application.Services;

namespace Upgradarr.Api.BackgroundServices;

public class CleanupBackgroundService : BackgroundService
{
    private readonly ILogger<CleanupBackgroundService> _logger;
    private readonly IOptionsMonitor<CleanupOptions> _optionsMonitor;
    private readonly IServiceProvider _serviceProvider;

    public CleanupBackgroundService(ILogger<CleanupBackgroundService> logger, IOptionsMonitor<CleanupOptions> optionsMonitor, IServiceProvider serviceProvider)
    {
        _logger = logger;
        _optionsMonitor = optionsMonitor;
        _serviceProvider = serviceProvider;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using (_logger.BeginScope("CleanupBackgroundService"))
        {
            _logger.LogStartingCleanupBackgroundService();
            stoppingToken.Register(() => _logger.LogStoppingCleanupBackgroundService());

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    using var scope = _serviceProvider.CreateScope();
                    var cleanupService = scope.ServiceProvider.GetRequiredService<CleanupService>();
                    await cleanupService.PerformCleanupAsync(stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogCleanupError(ex.Message);
                }

                await Task.Delay(TimeSpan.FromMinutes(_optionsMonitor.CurrentValue.CleanupIntervalMinutes), stoppingToken);
            }
        }
    }
}

internal static partial class CleanupLoggerExtensions
{
    [LoggerMessage(EventId = 0, Level = LogLevel.Information, Message = "Starting Cleanup Background Service.")]
    public static partial void LogStartingCleanupBackgroundService(this ILogger logger);

    [LoggerMessage(EventId = 1, Level = LogLevel.Information, Message = "Stopping Cleanup Background Service.")]
    public static partial void LogStoppingCleanupBackgroundService(this ILogger logger);

    [LoggerMessage(EventId = 2, Level = LogLevel.Error, Message = "An error occurred during cleanup: {ErrorMessage}")]
    public static partial void LogCleanupError(this ILogger logger, string errorMessage);
}
