using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Upgradarr.Domain.Entities;
using Upgradarr.Domain.Enums;
using Upgradarr.Domain.Interfaces;

namespace Upgradarr.Integrations.Interceptors;

public partial class DeleteQueueItemInterceptor(TimeProvider timeProvider, IServiceProvider serviceProvider, ILogger<DeleteQueueItemInterceptor> logger)
    : SaveChangesInterceptor
{
    private IUpgradeService UpgradeService => field ??= serviceProvider.GetRequiredService<IUpgradeService>();

    public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default
    )
    {
        if (result.HasResult)
        {
            return result;
        }

        if (eventData.Context is null)
        {
            return result;
        }

        var now = timeProvider.GetUtcNow();
        var entries = eventData
            .Context.ChangeTracker.Entries<QueueRecord>()
            .Where(e => e.Entity.RemoveAt.HasValue && e.Entity.RemoveAt.Value <= now)
            .GroupBy(e => e.Entity.Source);

        foreach (var group in entries)
        {
            var queueManager = serviceProvider.GetKeyedService<IQueueManager>(group.Key);
            if (queueManager is null)
            {
                LogErrorRemovingItemFromQueue(group.Key);
                continue;
            }

            foreach (var entity in group.Select(e => e.Entity))
            {
                var (allDeleted, itemsToRequeue) = await queueManager.DeleteQueueItemsAsync(entity, cancellationToken);

                if (allDeleted)
                {
                    eventData.Context.Remove(entity);
                }

                // Add items to front of upgrade queue
                if (itemsToRequeue.Count > 0)
                {
                    await UpgradeService.AddItemsToFrontOfQueueAsync(itemsToRequeue, cancellationToken);
                }
            }
        }

        return result;
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "No IQueueManager found for source {Source}. Cannot delete queue items.")]
    public partial void LogErrorRemovingItemFromQueue(RecordSource source);
}
