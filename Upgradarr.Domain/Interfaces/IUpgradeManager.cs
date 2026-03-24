using Upgradarr.Domain.Entities;
using Upgradarr.Domain.Enums;

namespace Upgradarr.Domain.Interfaces;

public interface IUpgradeManager
{
    /// <summary>
    /// The source this manager handles
    /// </summary>
    RecordSource SourceName { get; }

    /// <summary>
    /// Whether this manager can handle the specified item type
    /// </summary>
    bool CanHandle(ItemType itemType);

    /// <summary>
    /// Builds initial queue items for tracking
    /// </summary>
    Task<List<UpgradeState>> BuildQueueItemsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets newly added items that aren't currently being tracked
    /// </summary>
    Task<List<UpgradeState>> GetNewQueueItemsAsync(HashSet<int> existingIds, CancellationToken cancellationToken = default);

    /// <summary>
    /// Process the next action for a given item
    /// </summary>
    Task<UpgradeActionResult> ProcessUpgradeAsync(UpgradeState state, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if the item has an ongoing download
    /// </summary>
    Task<bool> HasOngoingDownloadAsync(UpgradeState state, CancellationToken cancellationToken = default);
}
