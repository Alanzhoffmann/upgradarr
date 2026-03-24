using Upgradarr.Apps.Models;

namespace Upgradarr.Apps.Interfaces;

public interface IQueueManager
{
    Task<(bool AllDeleted, List<ItemToQueue> ItemsToRequeue)> DeleteQueueItemsAsync(QueueRecord record, CancellationToken cancellationToken = default);
}
