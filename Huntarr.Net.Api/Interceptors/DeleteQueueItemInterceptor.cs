using Huntarr.Net.Api.Services;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Upgradarr.Apps.Enums;
using Upgradarr.Apps.Models;
using Upgradarr.Apps.Radarr;
using Upgradarr.Apps.Sonarr;

namespace Huntarr.Net.Api.Interceptors;

public class DeleteQueueItemInterceptor : ISaveChangesInterceptor
{
    private readonly TimeProvider _timeProvider;

    public DeleteQueueItemInterceptor(TimeProvider timeProvider)
    {
        _timeProvider = timeProvider;
    }

    public async ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default
    )
    {
        if (result.HasResult)
        {
            return result;
        }

        if (eventData.Context is not AppDbContext context)
        {
            return result;
        }

        var now = _timeProvider.GetUtcNow();
        var entries = context
            .ChangeTracker.Entries<QueueRecord>()
            .Where(e => e.Entity.RemoveAt.HasValue && e.Entity.RemoveAt.Value <= now)
            .GroupBy(e => e.Entity.Source);

        var upgradeService = context.GetService<UpgradeService>();

        foreach (var group in entries)
        {
            switch (group.Key)
            {
                case RecordSource.Sonarr:
                    await DeleteSonarrQueueItemsAsync(context, group.Select(e => e.Entity), upgradeService, cancellationToken);
                    break;
                case RecordSource.Radarr:
                    await DeleteRadarrQueueItemsAsync(context, group.Select(e => e.Entity), upgradeService, cancellationToken);
                    break;
            }
        }

        return result;
    }

    private static async Task DeleteRadarrQueueItemsAsync(
        AppDbContext context,
        IEnumerable<QueueRecord> enumerable,
        UpgradeService upgradeService,
        CancellationToken cancellationToken
    )
    {
        var radarrClient = context.GetService<RadarrClient>();
        foreach (var entity in enumerable)
        {
            var (allDeleted, itemsToRequeue) = await radarrClient.DeleteQueueItemsAsync(entity, cancellationToken);

            if (allDeleted)
            {
                context.Remove(entity);
            }

            // Add items to front of upgrade queue
            if (itemsToRequeue.Count > 0)
            {
                await upgradeService.AddItemsToFrontOfQueueAsync(itemsToRequeue, cancellationToken);
            }
        }
    }

    private static async Task DeleteSonarrQueueItemsAsync(
        AppDbContext context,
        IEnumerable<QueueRecord> entities,
        UpgradeService upgradeService,
        CancellationToken cancellationToken
    )
    {
        var sonarrClient = context.GetService<SonarrClient>();
        foreach (var entity in entities)
        {
            var (allDeleted, itemsToRequeue) = await sonarrClient.DeleteQueueItemsAsync(entity, cancellationToken);

            if (allDeleted)
            {
                context.Remove(entity);
            }

            // Add items to front of upgrade queue
            if (itemsToRequeue.Count > 0)
            {
                await upgradeService.AddItemsToFrontOfQueueAsync(itemsToRequeue, cancellationToken);
            }
        }
    }
}
