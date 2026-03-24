using Upgradarr.Domain.Entities;
using Upgradarr.Domain.ValueObjects;

namespace Upgradarr.Domain.Interfaces;

public interface IQueueManager
{
    ValueTask<(bool AllDeleted, List<ItemToQueue> ItemsToRequeue)> DeleteQueueItemsAsync(QueueRecord record, CancellationToken cancellationToken = default);
    IAsyncEnumerable<IQueueResource> GetAllQueueItems(CancellationToken cancellationToken = default);
    ValueTask<(bool ShouldRemove, int DownloadedScore)> ShouldRemoveImmediately(IQueueResource item, CancellationToken cancellationToken = default);
}
