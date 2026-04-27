using Upgradarr.Domain.Entities;
using Upgradarr.Domain.Enums;
using Upgradarr.Domain.ValueObjects;
using Upgradarr.Integrations.Interfaces;

namespace Upgradarr.Domain.Interfaces;

public interface IQueueManager
{
    RecordSource SourceName { get; }
    ValueTask<(bool AllDeleted, List<ItemToQueue> ItemsToRequeue)> DeleteQueueItemsAsync(QueueRecord record, CancellationToken cancellationToken = default);
    IAsyncEnumerable<IQueueResource> GetAllQueueItems(CancellationToken cancellationToken = default);
    ValueTask<(bool ShouldRemove, int DownloadedScore)> ShouldRemoveImmediately(IQueueResource item, CancellationToken cancellationToken = default);
}
