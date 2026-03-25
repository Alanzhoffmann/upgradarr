using Upgradarr.Contracts;

namespace Upgradarr.Application.Interfaces;

public interface IQueryService
{
    Task<List<UpgradeStateDto>> GetUpgradeStates(bool pendingOnly = false, CancellationToken cancellationToken = default);
    Task<List<QueueRecordDto>> GetTrackedDownloads(CancellationToken cancellationToken = default);
}
