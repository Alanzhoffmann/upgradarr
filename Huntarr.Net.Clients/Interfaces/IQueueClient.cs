using Huntarr.Net.Clients.Models;

namespace Huntarr.Net.Clients.Interfaces;

public interface IQueueClient<T> : IQueueClient
    where T : IQueueResource
{
    async Task<PagingResource<IQueueResource>> IQueueClient.GetQueueAsync(CancellationToken cancellationToken)
    {
        var result = await GetTypedQueueAsync(cancellationToken);
        return new PagingResource<IQueueResource>
        {
            Page = result.Page,
            PageSize = result.PageSize,
            Records = result.Records.Select(r => (IQueueResource)r),
            SortDirection = result.SortDirection,
            SortKey = result.SortKey,
            TotalRecords = result.TotalRecords,
        };
    }

    Task<PagingResource<T>> GetTypedQueueAsync(CancellationToken cancellationToken = default);
}

public interface IQueueClient
{
    Task<PagingResource<IQueueResource>> GetQueueAsync(CancellationToken cancellationToken = default);
    Task<bool> DeleteQueueItemAsync(int itemId, bool removeFromClient = true, bool blocklist = false, CancellationToken cancellationToken = default);
}
