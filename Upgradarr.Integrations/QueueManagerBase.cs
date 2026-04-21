using System.Runtime.CompilerServices;
using Microsoft.Extensions.Logging;
using Upgradarr.Domain.Entities;
using Upgradarr.Domain.Interfaces;
using Upgradarr.Domain.ValueObjects;
using Upgradarr.Integrations.Models;

namespace Upgradarr.Integrations;

public abstract class QueueManagerBase<TQueueResource>
    where TQueueResource : IQueueResource
{
    protected readonly ILogger _logger;

    protected QueueManagerBase(ILogger logger)
    {
        _logger = logger;
    }

    public async ValueTask<(bool AllDeleted, List<ItemToQueue> ItemsToRequeue)> DeleteQueueItemsAsync(
        QueueRecord record,
        CancellationToken cancellationToken = default
    )
    {
        var itemsToRequeue = new List<ItemToQueue>();
        var allDeleted = true;

        foreach (var itemScore in record.ItemScores)
        {
            if (
                !await DeleteQueueItemAsync(
                    itemScore.ItemId,
                    removeFromClient: true,
                    blocklist: true,
                    skipRedownload: true,
                    cancellationToken: cancellationToken
                )
            )
            {
                allDeleted = false;
            }
            else
            {
                itemsToRequeue.AddRange(await GetRequeueItemsAsync(itemScore.ItemId, cancellationToken));
            }
        }

        return (allDeleted, itemsToRequeue);
    }

    protected abstract Task<bool> DeleteQueueItemAsync(
        int itemId,
        bool removeFromClient,
        bool blocklist,
        bool skipRedownload,
        CancellationToken cancellationToken
    );

    protected abstract ValueTask<IEnumerable<ItemToQueue>> GetRequeueItemsAsync(int itemId, CancellationToken cancellationToken);

    public async IAsyncEnumerable<IQueueResource> GetAllQueueItems([EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        const int PageSize = 100;
        var page = 1;
        PagingResource<TQueueResource> items;
        do
        {
            items = await GetQueuePageAsync(page, PageSize, cancellationToken);
            foreach (var item in items.Records)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    yield break;
                }

                if (item.DownloadId is null)
                {
                    // Skip items with null DownloadId, as these cannot be tracked for cleanup
                    continue;
                }

                yield return await ProcessQueueItemForYieldAsync(item, cancellationToken);
            }

            page++;
        } while (items.TotalRecords > (page - 1) * PageSize);
    }

    protected abstract Task<PagingResource<TQueueResource>> GetQueuePageAsync(int page, int pageSize, CancellationToken cancellationToken);

    protected virtual ValueTask<TQueueResource> ProcessQueueItemForYieldAsync(TQueueResource item, CancellationToken cancellationToken) =>
        ValueTask.FromResult(item);
}
