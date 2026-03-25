using Upgradarr.Domain.Entities;
using Upgradarr.Domain.ValueObjects;

namespace Upgradarr.Domain.Interfaces;

public interface IUpgradeService
{
    Task AddItemsToFrontOfQueueAsync(IList<ItemToQueue> items, CancellationToken cancellationToken = default);
    Task<bool> ProcessItemUpgradeAsync(UpgradeState? state, CancellationToken cancellationToken = default);
    Task ProcessUpgradeAsync(CancellationToken cancellationToken = default);
}
