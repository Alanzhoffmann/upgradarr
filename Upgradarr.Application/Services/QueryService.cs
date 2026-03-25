using Microsoft.EntityFrameworkCore;
using Upgradarr.Application.Interfaces;
using Upgradarr.Contracts;
using Upgradarr.Data;
using Upgradarr.Domain.Entities;
using Upgradarr.Domain.Enums;

namespace Upgradarr.Application.Services;

public class QueryService : IQueryService
{
    private readonly AppDbContext _context;

    public QueryService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<List<QueueRecordDto>> GetTrackedDownloads(CancellationToken cancellationToken = default) =>
        await _context
            .TrackedDownloads.Select(q => new QueueRecordDto
            {
                DownloadId = q.DownloadId,
                Title = q.Title,
                Source = q.Source.ToString(),
                Added = q.Added,
                RemoveAt = q.RemoveAt,
            })
            .ToListAsync(cancellationToken: cancellationToken);

    public async Task<List<UpgradeStateDto>> GetUpgradeStates(bool pendingOnly = false, CancellationToken cancellationToken = default)
    {
        IQueryable<UpgradeState> query = _context.UpgradeStates.OrderBy(u => u.QueuePosition);

        if (pendingOnly)
        {
            query = query.Where(u => u.SearchState == SearchState.Pending);
        }

        return await query
            .Select(u => new UpgradeStateDto
            {
                Id = u.Id,
                Title = u.Title,
                ItemType = u.ItemType.ToString(),
                SearchState = u.SearchState.ToString(),
                QueuePosition = u.QueuePosition,
            })
            .ToListAsync(cancellationToken: cancellationToken);
    }
}
