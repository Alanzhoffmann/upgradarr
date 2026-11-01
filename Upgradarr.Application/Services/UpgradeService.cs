using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Upgradarr.Data;
using Upgradarr.Data.Interfaces;
using Upgradarr.Domain.Entities;
using Upgradarr.Domain.Enums;
using Upgradarr.Domain.Interfaces;
using Upgradarr.Domain.ValueObjects;
using Upgradarr.Integrations.Interfaces;

namespace Upgradarr.Application.Services;

internal partial class UpgradeService(
    IEnumerable<IUpgradeManager> upgradeManagers,
    AppDbContext dbContext,
    ILogger<UpgradeService> logger,
    TimeProvider timeProvider,
    IMigrationState migrationState
) : IUpgradeService
{
    public async Task ProcessUpgradeAsync(CancellationToken cancellationToken = default)
    {
        if (!migrationState.IsDone)
            return;

        UpgradeState? state;
        do
        {
            // Get the next item to process
            state = await GetNextItemToUpgradeAsync(cancellationToken);

            if (state is null)
            {
                LogNoMoreItemsToUpgrade();
                return;
            }
        } while (!await ProcessItemUpgradeAsync(state, cancellationToken));
    }

    public async Task RequeueAllItemsAsync(CancellationToken cancellationToken = default)
    {
        if (!migrationState.IsDone)
            return;

        await ResetQueueAsync(cancellationToken);
    }

    /// <summary>
    /// Gets the next item from the queue to upgrade
    /// </summary>
    private async Task<UpgradeState?> GetNextItemToUpgradeAsync(CancellationToken cancellationToken = default)
    {
        // Check for newly added items in Sonarr/Radarr and add them to queue
        await AddNewItemsToQueueAsync(cancellationToken);
        var now = timeProvider.GetUtcNow();

        // Get all pending items in queue order that are still monitored and released
        var pendingItems = await dbContext
            .UpgradeStates.Where(u => u.SearchState == SearchState.Pending)
            .OrderBy(u => u.QueuePosition)
            .ToListAsync(cancellationToken);

        // Check each item for ongoing downloads and skip those that have them
        foreach (var item in pendingItems.Where(u => u.IsMonitored).Where(u => u.ReleaseDate == null || u.ReleaseDate <= now))
        {
            if (!await HasOngoingDownloadAsync(item, cancellationToken))
            {
                return item;
            }
        }

        // Check if there are any searched items
        var hasSearchedItems = await dbContext.UpgradeStates.AnyAsync(u => u.SearchState == SearchState.Searched, cancellationToken);
        if (hasSearchedItems)
        {
            // All pending items processed, reset the queue and start over
            await ResetQueueAsync(cancellationToken);
            return null;
        }

        return null;
    }

    /// <summary>
    /// Process the next action for a given item
    /// </summary>
    public async Task<bool> ProcessItemUpgradeAsync(UpgradeState? state, CancellationToken cancellationToken = default)
    {
        if (state is null)
            return false;

        LogProcessingItem(state.ItemType, state.Title ?? "Unknown", state.ItemId);

        var manager = upgradeManagers.FirstOrDefault(m => m.CanHandle(state.ItemType));
        if (manager is null)
        {
            return false;
        }

        try
        {
            var result = await manager.ProcessUpgradeAsync(state, cancellationToken);
            switch (result)
            {
                case UpgradeActionResult.Searched:
                    state.SearchState = SearchState.Searched;
                    state.LastUpdatedAt = timeProvider.GetUtcNow();
                    await dbContext.SaveChangesAsync(cancellationToken);
                    return true;

                case UpgradeActionResult.Removed:
                    await RemoveItemAndChildrenAsync(state, cancellationToken);
                    return false;

                case UpgradeActionResult.Skipped:
                default:
                    return false;
            }
        }
        catch (Exception ex)
        {
            LogErrorFailedToProcessUpgrade(ex, state.ItemId);
            return false;
        }
    }

    private async Task RemoveItemAndChildrenAsync(UpgradeState state, CancellationToken cancellationToken)
    {
        var itemsToRemove = new List<UpgradeState> { state };

        if (state.ItemType == ItemType.Series)
        {
            var children = await dbContext.UpgradeStates.Where(u => u.ParentSeriesId == state.ItemId).ToListAsync(cancellationToken);
            itemsToRemove.AddRange(children);
        }
        else if (state.ItemType == ItemType.Season)
        {
            var children = await dbContext
                .UpgradeStates.Where(u => u.ParentSeriesId == state.ParentSeriesId && u.SeasonNumber == state.SeasonNumber)
                .ToListAsync(cancellationToken);
            // It will also include the season itself
            itemsToRemove.AddRange(children);
        }

        dbContext.UpgradeStates.RemoveRange(itemsToRemove.DistinctBy(i => i.Id));
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task<bool> HasOngoingDownloadAsync(UpgradeState state, CancellationToken cancellationToken = default)
    {
        var manager = upgradeManagers.FirstOrDefault(m => m.CanHandle(state.ItemType));
        if (manager is null)
            return false;

        try
        {
            return await manager.HasOngoingDownloadAsync(state, cancellationToken);
        }
        catch (Exception ex)
        {
            LogErrorCheckingQueueForItem(ex, state.ItemId);
            return false;
        }
    }

    private static List<UpgradeState> ShuffleAndAssignPositions(List<UpgradeState> items)
    {
        var shuffled = ShuffleQueue(items);
        for (int i = 0; i < shuffled.Count; i++)
        {
            shuffled[i].QueuePosition = i;
        }
        return shuffled;
    }

    /// <summary>
    /// Shuffles the queue while maintaining the constraint that seasons cannot appear before their series
    /// and episodes cannot appear before their series or season.
    /// Prioritizes missing items (IsMissing = true) before items due for upgrade.
    /// </summary>
    private static List<UpgradeState> ShuffleQueue(List<UpgradeState> items)
    {
        // Separate missing and non-missing items
        var missingItems = items.Where(i => i.IsMissing).ToList();
        var upgradeItems = items.Where(i => !i.IsMissing).ToList();

        // Shuffle each group separately
        return [.. ShuffleWithDependencies(missingItems), .. ShuffleWithDependencies(upgradeItems)];
    }

    /// <summary>
    /// Shuffles items while respecting parent-child dependencies.
    /// </summary>
    private static List<UpgradeState> ShuffleWithDependencies(List<UpgradeState> items)
    {
        var result = new List<UpgradeState>();
        var remaining = new List<UpgradeState>(items);

        // Track what's been added
        var addedSeries = new HashSet<int>();
        var addedSeasons = new HashSet<(int, int)>(); // (SeriesId, SeasonNumber)

        while (remaining.Count > 0)
        {
            // Find all items that can be added (dependencies are met)
            var available = remaining
                .Where(item =>
                {
                    return item.ItemType switch
                    {
                        ItemType.Series or ItemType.Movie => true,
                        ItemType.Season => item.ParentSeriesId.HasValue && addedSeries.Contains(item.ParentSeriesId.Value),
                        ItemType.Episode => item.ParentSeriesId.HasValue
                            && addedSeries.Contains(item.ParentSeriesId.Value)
                            && item.SeasonNumber.HasValue
                            && addedSeasons.Contains((item.ParentSeriesId.Value, item.SeasonNumber.Value)),
                        _ => false,
                    };
                })
                .ToList();

            if (available.Count == 0)
            {
                // Shouldn't happen with valid data, but add remaining to avoid infinite loop
                result.AddRange(remaining);
                break;
            }

            // Shuffle available items and pick one
            var shuffled = available.OrderBy(_ => Random.Shared.Next()).First();
            result.Add(shuffled);
            remaining.Remove(shuffled);

            // Update tracking
            if (shuffled.ItemType == ItemType.Series)
            {
                addedSeries.Add(shuffled.ItemId);
            }
            else if (shuffled.ItemType == ItemType.Season && shuffled.ParentSeriesId.HasValue && shuffled.SeasonNumber.HasValue)
            {
                addedSeasons.Add((shuffled.ParentSeriesId.Value, shuffled.SeasonNumber.Value));
            }
        }

        return result;
    }

    private async Task ResetQueueAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            // Clear existing queue
            var allStates = await dbContext.UpgradeStates.ToListAsync(cancellationToken);
            dbContext.UpgradeStates.RemoveRange(allStates);
            await dbContext.SaveChangesAsync(cancellationToken);

            var queueItems = new List<UpgradeState>();
            foreach (var manager in upgradeManagers)
            {
                await foreach (var item in manager.BuildQueueItemsAsync(cancellationToken))
                {
                    queueItems.Add(item);
                }
            }

            var shuffledQueue = ShuffleAndAssignPositions(queueItems);

            dbContext.UpgradeStates.AddRange(shuffledQueue);
            await dbContext.SaveChangesAsync(cancellationToken);
            LogResetQueuePosition();
        }
        catch (Exception ex)
        {
            LogErrorResettingQueue(ex);
        }
    }

    private async Task AddNewItemsToQueueAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var existingItems = await dbContext.UpgradeStates.ToListAsync(cancellationToken);

            var allTrackableItems = new List<UpgradeState>();
            foreach (var manager in upgradeManagers)
            {
                await foreach (var item in manager.BuildQueueItemsAsync(cancellationToken))
                {
                    allTrackableItems.Add(item);
                }
            }

            var trackableLookup = allTrackableItems.ToDictionary(t => (t.ItemType, t.ItemId, t.ParentSeriesId, t.SeasonNumber, t.EpisodeNumber));
            bool monitoringChanged = false;

            foreach (var item in existingItems)
            {
                bool isCurrentlyMonitored = trackableLookup.ContainsKey(
                    (item.ItemType, item.ItemId, item.ParentSeriesId, item.SeasonNumber, item.EpisodeNumber)
                );

                if (item.IsMonitored != isCurrentlyMonitored)
                {
                    item.IsMonitored = isCurrentlyMonitored;
                    item.LastUpdatedAt = timeProvider.GetUtcNow();
                    monitoringChanged = true;
                }
            }

            var existingKeys = existingItems.Select(i => (i.ItemType, i.ItemId, i.ParentSeriesId, i.SeasonNumber, i.EpisodeNumber)).ToHashSet();
            var newItems = allTrackableItems
                .Where(t => !existingKeys.Contains((t.ItemType, t.ItemId, t.ParentSeriesId, t.SeasonNumber, t.EpisodeNumber)))
                .ToList();

            if (newItems.Count == 0 && !monitoringChanged)
                return;

            var allItems = existingItems.Concat(newItems).ToList();
            var shuffled = ShuffleQueue(allItems);

            // Reassign queue positions for all items
            for (int i = 0; i < shuffled.Count; i++)
            {
                shuffled[i].QueuePosition = i;
            }

            // Update existing items with new positions
            foreach (var item in shuffled.Where(i => existingItems.Any(e => e.Id == i.Id)))
            {
                var existing = existingItems.First(e => e.Id == item.Id);
                existing.QueuePosition = item.QueuePosition;
            }

            // Add new items to context
            if (newItems.Count > 0)
            {
                dbContext.UpgradeStates.AddRange(newItems);
            }

            await dbContext.SaveChangesAsync(cancellationToken);

            if (newItems.Count > 0)
            {
                LogAddedNewItemsToQueue(newItems.Count);
            }

            if (monitoringChanged)
            {
                var changedCount = existingItems.Count(i => dbContext.Entry(i).Property(nameof(UpgradeState.IsMonitored)).IsModified);
                if (changedCount > 0)
                {
                    LogUpdatedMonitoringStatus(changedCount);
                }
            }
        }
        catch (Exception ex)
        {
            LogErrorAddingNewItemsToQueue(ex);
        }
    }

    public async Task AddItemsToFrontOfQueueAsync(IList<ItemToQueue> items, CancellationToken cancellationToken = default)
    {
        try
        {
            var allItems = await dbContext.UpgradeStates.ToListAsync(cancellationToken);

            foreach (var item in allItems)
            {
                item.QueuePosition += items.Count;
            }

            int position = 0;
            foreach (var (itemType, itemId, parentSeriesId, seasonNumber, episodeNumber) in items)
            {
                var existingItem = await dbContext.UpgradeStates.FirstOrDefaultAsync(u => u.ItemId == itemId && u.ItemType == itemType, cancellationToken);

                if (existingItem is not null)
                {
                    existingItem.SearchState = SearchState.Pending;
                    existingItem.QueuePosition = position++;
                    existingItem.LastUpdatedAt = timeProvider.GetUtcNow();
                }
            }

            dbContext.UpgradeStates.UpdateRange(allItems);
            await dbContext.SaveChangesAsync(cancellationToken);

            LogAddedItemsToFrontOfQueue(items.Count);
        }
        catch (Exception ex)
        {
            LogErrorAddingItemsToQueue(ex);
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "No more items to upgrade")]
    public partial void LogNoMoreItemsToUpgrade();

    [LoggerMessage(Level = LogLevel.Information, Message = "Processing {ItemType}: {Title} (ID: {ItemId})")]
    public partial void LogProcessingItem(ItemType itemType, string title, int itemId);

    [LoggerMessage(Level = LogLevel.Error, Message = "Error checking queue for item {ItemId}")]
    public partial void LogErrorCheckingQueueForItem(Exception ex, int itemId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Reset queue position")]
    public partial void LogResetQueuePosition();

    [LoggerMessage(Level = LogLevel.Error, Message = "Error resetting queue")]
    public partial void LogErrorResettingQueue(Exception ex);

    [LoggerMessage(Level = LogLevel.Information, Message = "Added {ItemCount} items to front of queue")]
    public partial void LogAddedItemsToFrontOfQueue(int itemCount);

    [LoggerMessage(Level = LogLevel.Error, Message = "Error adding items to queue")]
    public partial void LogErrorAddingItemsToQueue(Exception ex);

    [LoggerMessage(Level = LogLevel.Information, Message = "Added {ItemCount} new items to queue and reshuffled")]
    public partial void LogAddedNewItemsToQueue(int itemCount);

    [LoggerMessage(Level = LogLevel.Information, Message = "Updated monitoring status for {ItemCount} items")]
    public partial void LogUpdatedMonitoringStatus(int itemCount);

    [LoggerMessage(Level = LogLevel.Error, Message = "Error detecting and adding new items to queue")]
    public partial void LogErrorAddingNewItemsToQueue(Exception ex);

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to process upgrade for item {ItemId}")]
    public partial void LogErrorFailedToProcessUpgrade(Exception ex, int itemId);
}
