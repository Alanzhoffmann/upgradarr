using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Upgradarr.Application.Extensions;
using Upgradarr.Application.Services;
using Upgradarr.Apps.Radarr;
using Upgradarr.Apps.Radarr.Models;
using Upgradarr.Apps.Sonarr;
using Upgradarr.Apps.Sonarr.Models;
using Upgradarr.Data;
using Upgradarr.Domain.Entities;
using Upgradarr.Domain.Enums;
using Upgradarr.Domain.Interfaces;
using Upgradarr.Domain.ValueObjects;

namespace Upgradarr.Application.Services;

public class UpgradeService : IUpgradeService
{
    private readonly SonarrClient _sonarrClient;
    private readonly RadarrClient _radarrClient;
    private readonly AppDbContext _dbContext;
    private readonly ILogger<UpgradeService> _logger;
    private readonly TimeProvider _timeProvider;

    public UpgradeService(
        SonarrClient sonarrClient,
        RadarrClient radarrClient,
        AppDbContext dbContext,
        ILogger<UpgradeService> logger,
        TimeProvider timeProvider
    )
    {
        _sonarrClient = sonarrClient;
        _radarrClient = radarrClient;
        _dbContext = dbContext;
        _logger = logger;
        _timeProvider = timeProvider;
    }

    public async Task ProcessUpgradeAsync(CancellationToken cancellationToken = default)
    {
        UpgradeState? state;
        do
        {
            // Get the next item to process
            state = await GetNextItemToUpgradeAsync(cancellationToken);

            if (state is null)
            {
                _logger.LogNoMoreItemsToUpgrade();
                return;
            }
        } while (!await ProcessItemUpgradeAsync(state, cancellationToken));
    }

    /// <summary>
    /// Gets the next item from the queue to upgrade
    /// </summary>
    private async Task<UpgradeState?> GetNextItemToUpgradeAsync(CancellationToken cancellationToken = default)
    {
        // Check for newly added items in Sonarr/Radarr and add them to queue
        await AddNewItemsToQueueAsync(cancellationToken);
        var now = _timeProvider.GetUtcNow();

        // Get all pending items in queue order that are still monitored and released
        var pendingItems = await _dbContext
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
        var hasSearchedItems = await _dbContext.UpgradeStates.AnyAsync(u => u.SearchState == SearchState.Searched, cancellationToken);
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

        _logger.LogProcessingItem(state.ItemType, state.Title ?? "Unknown", state.ItemId);

        return state.ItemType switch
        {
            ItemType.Series => await ProcessSeriesUpgradeAsync(state, cancellationToken),
            ItemType.Season => await ProcessSeasonUpgradeAsync(state, cancellationToken),
            ItemType.Episode => await ProcessEpisodeUpgradeAsync(state, cancellationToken),
            ItemType.Movie => await ProcessMovieUpgradeAsync(state, cancellationToken),
            _ => false,
        };
    }

    private async Task<bool> HasOngoingDownloadAsync(UpgradeState state, CancellationToken cancellationToken = default)
    {
        try
        {
            return state.ItemType switch
            {
                ItemType.Series => await HasSeriesOngoingDownloadAsync(state, cancellationToken),
                ItemType.Movie => await HasMovieOngoingDownloadAsync(state, cancellationToken),
                ItemType.Season => await HasSeasonOngoingDownloadAsync(state, cancellationToken),
                ItemType.Episode => await HasEpisodeOngoingDownloadAsync(state, cancellationToken),
                _ => false,
            };
        }
        catch (Exception ex)
        {
            _logger.LogErrorCheckingQueueForItem(ex, state.ItemId);
        }

        return false;
    }

    private async Task<bool> HasSeriesOngoingDownloadAsync(UpgradeState state, CancellationToken cancellationToken = default)
    {
        var queue = _sonarrClient.GetAllQueueItems(cancellationToken);
        if (await queue.AnyAsync(q => q is SonarrQueueResource sonarrResource && sonarrResource.SeriesId == state.ItemId, cancellationToken: cancellationToken))
        {
            _logger.LogSeriesHasOngoingDownloads(state.Title ?? "Unknown", state.ItemId);
            return true;
        }

        return false;
    }

    private async Task<bool> HasMovieOngoingDownloadAsync(UpgradeState state, CancellationToken cancellationToken = default)
    {
        var queue = _radarrClient.GetAllQueueItems(cancellationToken);
        if (await queue.AnyAsync(q => q is RadarrQueueResource radarrResource && radarrResource.MovieId == state.ItemId, cancellationToken: cancellationToken))
        {
            _logger.LogMovieIsDownloading(state.Title ?? "Unknown", state.ItemId);
            return true;
        }

        return false;
    }

    private async Task<bool> HasSeasonOngoingDownloadAsync(UpgradeState state, CancellationToken cancellationToken = default)
    {
        if (!state.ParentSeriesId.HasValue || !state.SeasonNumber.HasValue)
            return false;

        var queue = _sonarrClient.GetAllQueueItems(cancellationToken);
        if (
            await queue.AnyAsync(
                q =>
                    q is SonarrQueueResource sonarrResource
                    && sonarrResource.SeriesId == state.ParentSeriesId.Value
                    && sonarrResource.Episode?.SeasonNumber == state.SeasonNumber.Value,
                cancellationToken: cancellationToken
            )
        )
        {
            _logger.LogSeasonHasOngoingDownloads(state.SeasonNumber.Value, state.Title ?? "Unknown", state.ParentSeriesId.Value);
            return true;
        }

        return false;
    }

    private async Task<bool> HasEpisodeOngoingDownloadAsync(UpgradeState state, CancellationToken cancellationToken = default)
    {
        if (!state.ParentSeriesId.HasValue || !state.SeasonNumber.HasValue || !state.EpisodeNumber.HasValue)
            return false;

        var queue = _sonarrClient.GetAllQueueItems(cancellationToken);
        if (
            await queue.AnyAsync(q => q is SonarrQueueResource sonarrResource && sonarrResource.EpisodeId == state.ItemId, cancellationToken: cancellationToken)
        )
        {
            _logger.LogEpisodeIsDownloading(state.Title ?? "Unknown", state.SeasonNumber.Value, state.EpisodeNumber.Value, state.ItemId);
            return true;
        }

        return false;
    }

    private async Task<bool> ProcessSeriesUpgradeAsync(UpgradeState state, CancellationToken cancellationToken = default)
    {
        try
        {
            var series = await _sonarrClient.GetSeriesByIdAsync(state.ItemId, cancellationToken: cancellationToken);
            if (series is null)
            {
                _logger.LogSeriesNotFound(state.ItemId);
                state.SearchState = SearchState.Searched;
                state.LastUpdatedAt = _timeProvider.GetUtcNow();
                await _dbContext.SaveChangesAsync(cancellationToken);
                return false;
            }

            // Check if still monitored
            if (!series.Monitored)
            {
                _logger.LogSeriesNoLongerMonitored(series.Title ?? "Unknown", state.ItemId);
                await RemoveItemFromQueueAsync(state.ItemId, ItemType.Series, cancellationToken);
                return false;
            }

            _logger.LogSearchingForSeries(series.Title ?? "Unknown");
            await ExecuteSeriesSearchAsync(state.ItemId, cancellationToken);
            state.SearchState = SearchState.Searched;
            state.LastUpdatedAt = _timeProvider.GetUtcNow();
            await _dbContext.SaveChangesAsync(cancellationToken);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogErrorProcessingSeriesUpgrade(ex, state.ItemId);
        }

        return false;
    }

    private async Task<bool> ProcessSeasonUpgradeAsync(UpgradeState state, CancellationToken cancellationToken = default)
    {
        try
        {
            if (!state.ParentSeriesId.HasValue || !state.SeasonNumber.HasValue)
                return false;

            var series = await _sonarrClient.GetSeriesByIdAsync(state.ParentSeriesId.Value, cancellationToken: cancellationToken);
            if (series is null)
                return false;

            // Check if still monitored
            if (!series.Monitored)
            {
                _logger.LogSeriesNoLongerMonitored(series.Title ?? "Unknown", state.ParentSeriesId.Value);
                await RemoveSeasonFromQueueAsync(state.ParentSeriesId.Value, state.SeasonNumber.Value, cancellationToken);
                return false;
            }

            // Check if season has ongoing downloads
            if (await HasSeasonOngoingDownloadAsync(state, cancellationToken))
            {
                return false;
            }

            _logger.LogSearchingForSeason(series.Title ?? "Unknown", state.SeasonNumber.Value);
            await ExecuteSeasonSearchAsync(state.ParentSeriesId.Value, state.SeasonNumber.Value, cancellationToken);
            state.SearchState = SearchState.Searched;
            state.LastUpdatedAt = _timeProvider.GetUtcNow();
            await _dbContext.SaveChangesAsync(cancellationToken);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogErrorProcessingSeasonUpgrade(ex, state.ParentSeriesId ?? 0, state.SeasonNumber ?? 0);
        }

        return false;
    }

    private async Task<bool> ProcessEpisodeUpgradeAsync(UpgradeState state, CancellationToken cancellationToken = default)
    {
        try
        {
            if (!state.ParentSeriesId.HasValue || !state.SeasonNumber.HasValue || !state.EpisodeNumber.HasValue)
                return false;

            var series = await _sonarrClient.GetSeriesByIdAsync(state.ParentSeriesId.Value, cancellationToken: cancellationToken);
            if (series is null)
                return false;

            // Check if still monitored
            if (!series.Monitored)
            {
                _logger.LogSeriesNoLongerMonitored(series.Title ?? "Unknown", state.ParentSeriesId.Value);
                await RemoveItemFromQueueAsync(state.ItemId, ItemType.Episode, cancellationToken);
                return false;
            }

            var episodes = await _sonarrClient.GetEpisodesAsync(
                seriesId: state.ParentSeriesId.Value,
                seasonNumber: state.SeasonNumber.Value,
                cancellationToken: cancellationToken
            );
            var episode = episodes.FirstOrDefault(e => e.SeasonNumber == state.SeasonNumber && e.EpisodeNumber == state.EpisodeNumber);

            if (episode is null || !episode.Monitored)
            {
                _logger.LogEpisodeNotFoundOrUnmonitored(series.Title ?? "Unknown", state.SeasonNumber.Value, state.EpisodeNumber.Value, state.ItemId);
                await RemoveItemFromQueueAsync(state.ItemId, ItemType.Episode, cancellationToken);
                return false;
            }

            // Check if episode has ongoing downloads
            if (await HasEpisodeOngoingDownloadAsync(state, cancellationToken))
            {
                return false;
            }

            _logger.LogSearchingForEpisode(
                series.Title ?? "Unknown",
                state.SeasonNumber.Value.ToString().PadLeft(2, '0'),
                state.EpisodeNumber.Value.ToString().PadLeft(2, '0')
            );
            await ExecuteEpisodeSearchAsync([episode.Id], cancellationToken);
            state.SearchState = SearchState.Searched;
            state.LastUpdatedAt = _timeProvider.GetUtcNow();
            await _dbContext.SaveChangesAsync(cancellationToken);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogErrorProcessingEpisodeUpgrade(ex, state.ItemId);
        }

        return false;
    }

    private async Task<bool> ProcessMovieUpgradeAsync(UpgradeState state, CancellationToken cancellationToken = default)
    {
        try
        {
            var movie = await _radarrClient.GetMovieByIdAsync(state.ItemId, cancellationToken);
            if (movie is null)
            {
                _logger.LogMovieNotFound(state.ItemId);
                state.SearchState = SearchState.Searched;
                await _dbContext.SaveChangesAsync(cancellationToken);
                return false;
            }

            // Check if still monitored
            if (!movie.Monitored)
            {
                _logger.LogMovieNoLongerMonitored(movie.Title ?? "Unknown", state.ItemId);
                await RemoveItemFromQueueAsync(state.ItemId, ItemType.Movie, cancellationToken);
                return false;
            }

            _logger.LogSearchingForMovie(movie.Title ?? "Unknown");
            await ExecuteMovieSearchAsync([state.ItemId], cancellationToken);
            state.SearchState = SearchState.Searched;
            state.LastUpdatedAt = _timeProvider.GetUtcNow();
            await _dbContext.SaveChangesAsync(cancellationToken);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogErrorProcessingMovieUpgrade(ex, state.ItemId);
        }

        return false;
    }

    /// <summary>
    /// Initialize upgrade tracking with all monitored series and movies in a shuffled queue.
    /// For shows, creates entries for the show, all seasons, and all monitored episodes.
    /// Shows and movies are shuffled together, but seasons follow their shows and episodes follow their seasons.
    /// </summary>
    public async Task InitializeUpgradeStatesAsync(CancellationToken cancellationToken = default)
    {
        // Check if already initialized
        if (await _dbContext.UpgradeStates.AnyAsync(cancellationToken))
        {
            return;
        }

        try
        {
            var series = await _sonarrClient.GetSeriesAsync(cancellationToken);
            var movies = await _radarrClient.GetMoviesAsync(cancellationToken: cancellationToken);

            var queueItems = await BuildQueueItemsAsync(series.Where(s => s.Monitored), movies.Where(m => m.Monitored), cancellationToken);
            var shuffledQueue = ShuffleAndAssignPositions(queueItems);

            _dbContext.UpgradeStates.AddRange(shuffledQueue);
            await _dbContext.SaveChangesAsync(cancellationToken);
            _logger.LogInitializedUpgradeStates(series.Count(s => s.Monitored), movies.Count(m => m.Monitored));
        }
        catch (Exception ex)
        {
            _logger.LogErrorInitializingUpgradeStates(ex);
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

    /// <summary>
    /// Builds queue items from a list of series and movies.
    /// Handles series→season→episode hierarchy with missing file detection.
    /// Sets IsMissing status immediately when a missing episode is detected.
    /// </summary>
    private async Task<List<UpgradeState>> BuildQueueItemsAsync(
        IEnumerable<SeriesResource> series,
        IEnumerable<MovieResource> movies,
        CancellationToken cancellationToken = default
    )
    {
        var queueItems = new List<UpgradeState>();

        // Create entries for all monitored series
        foreach (var s in series)
        {
            // Skip unreleased series
            if (s.FirstAired.HasValue && s.FirstAired.Value > _timeProvider.GetUtcNow())
                continue;

            // Add series entry
            var seriesItem = new UpgradeState
            {
                ItemId = s.Id,
                Title = s.Title,
                ItemType = ItemType.Series,
                SearchState = SearchState.Pending,
                IsMonitored = true,
                ReleaseDate = s.FirstAired,
                CreatedAt = _timeProvider.GetUtcNow(),
            };
            queueItems.Add(seriesItem);

            // Get all episodes for this series
            var episodes = await _sonarrClient.GetEpisodesAsync(seriesId: s.Id, cancellationToken: cancellationToken);

            // Group by season
            var seasonGroups = episodes
                .Where(e => e.Monitored) // Only monitored episodes
                .GroupBy(e => e.SeasonNumber)
                .OrderBy(g => g.Key)
                .ToList();

            foreach (var seasonGroup in seasonGroups)
            {
                int seasonNumber = seasonGroup.Key;

                // Add season entry
                var seasonItem = new UpgradeState
                {
                    ItemId = seasonNumber, // Use season number as ID for uniqueness with parent
                    ParentSeriesId = s.Id,
                    SeasonNumber = seasonNumber,
                    Title = $"{s.Title} - Season {seasonNumber}",
                    ItemType = ItemType.Season,
                    SearchState = SearchState.Pending,
                    IsMonitored = true,
                    ReleaseDate = seasonGroup.Min(e => e.AirDateUtc),
                    CreatedAt = _timeProvider.GetUtcNow(),
                };
                queueItems.Add(seasonItem);

                // Add episode entries
                foreach (var episode in seasonGroup.OrderBy(e => e.EpisodeNumber))
                {
                    // Skip unreleased episodes
                    if (episode.AirDateUtc.HasValue && episode.AirDateUtc.Value > _timeProvider.GetUtcNow())
                        continue;

                    // Check if episode is missing (has no file)
                    bool isMissing = !episode.HasFile;

                    var episodeItem = new UpgradeState
                    {
                        ItemId = episode.Id,
                        ParentSeriesId = s.Id,
                        SeasonNumber = seasonNumber,
                        EpisodeNumber = episode.EpisodeNumber,
                        Title = $"{s.Title} - S{seasonNumber:D2}E{episode.EpisodeNumber:D2}",
                        ItemType = ItemType.Episode,
                        SearchState = SearchState.Pending,
                        IsMonitored = true,
                        IsMissing = isMissing,
                        ReleaseDate = episode.AirDateUtc,
                        CreatedAt = _timeProvider.GetUtcNow(),
                    };
                    queueItems.Add(episodeItem);

                    // If episode is missing, mark season and series as missing immediately
                    if (isMissing)
                    {
                        seasonItem.IsMissing = true;
                        seriesItem.IsMissing = true;
                    }
                }
            }
        }

        // Create entries for all monitored movies
        foreach (var m in movies)
        {
            // Skip unreleased movies
            if (m.InCinemas.HasValue && m.InCinemas.Value > _timeProvider.GetUtcNow())
                continue;

            // Check if movie is missing (has no file) - set IsMissing at point of creation
            bool isMissing = !m.HasFile;

            queueItems.Add(
                new UpgradeState
                {
                    ItemId = m.Id,
                    Title = m.Title,
                    ItemType = ItemType.Movie,
                    SearchState = SearchState.Pending,
                    IsMonitored = true,
                    IsMissing = isMissing,
                    ReleaseDate = m.InCinemas,
                    CreatedAt = _timeProvider.GetUtcNow(),
                }
            );
        }

        return queueItems;
    }

    /// <summary>
    /// Reset the queue to restart processing - completely reinitializes from source systems
    /// This allows picking up any newly added items in Sonarr/Radarr
    /// </summary>
    private async Task ResetQueueAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            // Clear existing queue
            var allStates = await _dbContext.UpgradeStates.ToListAsync(cancellationToken);
            _dbContext.UpgradeStates.RemoveRange(allStates);
            await _dbContext.SaveChangesAsync(cancellationToken);

            // Reinitialize with fresh data from sources
            var series = await _sonarrClient.GetSeriesAsync(cancellationToken);
            var movies = await _radarrClient.GetMoviesAsync(cancellationToken: cancellationToken);

            var queueItems = await BuildQueueItemsAsync(series.Where(s => s.Monitored), movies.Where(m => m.Monitored), cancellationToken);
            var shuffledQueue = ShuffleAndAssignPositions(queueItems);

            _dbContext.UpgradeStates.AddRange(shuffledQueue);
            await _dbContext.SaveChangesAsync(cancellationToken);
            _logger.LogResetQueuePosition();
        }
        catch (Exception ex)
        {
            _logger.LogErrorResettingQueue(ex);
        }
    }

    /// <summary>
    /// Check for new items in Sonarr/Radarr and add them to the queue if not already present
    /// This allows the queue to pick up newly added content during runtime
    /// </summary>
    private async Task AddNewItemsToQueueAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var existingItems = await _dbContext.UpgradeStates.ToListAsync(cancellationToken);
            var existingSeriesIds = new HashSet<int>(existingItems.Where(u => u.ItemType == ItemType.Series).Select(u => u.ItemId));
            var existingMovieIds = new HashSet<int>(existingItems.Where(u => u.ItemType == ItemType.Movie).Select(u => u.ItemId));

            var series = await _sonarrClient.GetSeriesAsync(cancellationToken);
            var movies = await _radarrClient.GetMoviesAsync(cancellationToken: cancellationToken);

            // Update monitoring status for existing items
            var seriesLookup = series.ToDictionary(s => s.Id, s => s.Monitored);
            var moviesLookup = movies.ToDictionary(m => m.Id, m => m.Monitored);
            bool monitoringChanged = false;

            foreach (var item in existingItems)
            {
                bool currentMonitoringStatus =
                    item.ItemType == ItemType.Series ? (seriesLookup.TryGetValue(item.ItemId, out var seriesMonitored) && seriesMonitored)
                    : item.ItemType == ItemType.Movie ? (moviesLookup.TryGetValue(item.ItemId, out var movieMonitored) && movieMonitored)
                    : item.ItemType is ItemType.Season or ItemType.Episode
                        && item.ParentSeriesId.HasValue
                        && seriesLookup.TryGetValue(item.ParentSeriesId.Value, out var parentMonitored)
                        && parentMonitored;

                if (item.IsMonitored != currentMonitoringStatus)
                {
                    item.IsMonitored = currentMonitoringStatus;
                    item.LastUpdatedAt = _timeProvider.GetUtcNow();
                    monitoringChanged = true;
                }
            }

            // Filter to only new monitored items
            var newSeries = series.Where(s => s.Monitored && !existingSeriesIds.Contains(s.Id)).ToList();
            var newMovies = movies.Where(m => m.Monitored && !existingMovieIds.Contains(m.Id)).ToList();

            bool hasNewItems = newSeries.Count > 0 || newMovies.Count > 0;

            // If no new items and no monitoring changes, return early
            if (!hasNewItems && !monitoringChanged)
                return;

            // Build queue items for only the new series/movies
            var newItems = hasNewItems ? await BuildQueueItemsAsync(newSeries, newMovies, cancellationToken) : [];

            // If no queue items were built and no monitoring changed, return early
            if (newItems.Count == 0 && !monitoringChanged)
                return;

            // Combine existing and new items
            var allItems = existingItems.Concat(newItems).ToList();

            // Reshuffle to integrate new items with missing items prioritized
            // Missing items are already marked during queue building
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
                _dbContext.UpgradeStates.AddRange(newItems);
            }

            await _dbContext.SaveChangesAsync(cancellationToken);

            if (newItems.Count > 0)
            {
                _logger.LogAddedNewItemsToQueue(newItems.Count);
            }

            if (monitoringChanged)
            {
                var changedCount = existingItems.Count(i => _dbContext.Entry(i).Property(nameof(UpgradeState.IsMonitored)).IsModified);
                _logger.LogUpdatedMonitoringStatus(changedCount);
            }
        }
        catch (Exception ex)
        {
            _logger.LogErrorAddingNewItemsToQueue(ex);
        }
    }

    /// <summary>
    /// Remove a single item from the queue
    /// </summary>
    private async Task RemoveItemFromQueueAsync(int itemId, ItemType itemType, CancellationToken cancellationToken = default)
    {
        try
        {
            var item = await _dbContext.UpgradeStates.FirstOrDefaultAsync(u => u.ItemId == itemId && u.ItemType == itemType, cancellationToken);

            if (item is not null)
            {
                _dbContext.UpgradeStates.Remove(item);
                await _dbContext.SaveChangesAsync(cancellationToken);
            }
        }
        catch (Exception ex)
        {
            _logger.LogErrorRemovingItemFromQueue(ex, itemId);
        }
    }

    /// <summary>
    /// Remove all season and episode entries for a series
    /// </summary>
    private async Task RemoveSeasonFromQueueAsync(int seriesId, int seasonNumber, CancellationToken cancellationToken = default)
    {
        try
        {
            var seasonItems = await _dbContext
                .UpgradeStates.Where(u => u.ParentSeriesId == seriesId && u.SeasonNumber == seasonNumber)
                .ToListAsync(cancellationToken);

            _dbContext.UpgradeStates.RemoveRange(seasonItems);
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogErrorRemovingSeasonFromQueue(ex, seriesId, seasonNumber);
        }
    }

    /// <summary>
    /// Add items to the front of the queue (for cleanup re-processing)
    /// </summary>
    public async Task AddItemsToFrontOfQueueAsync(IList<ItemToQueue> items, CancellationToken cancellationToken = default)
    {
        try
        {
            // Get all current items and shift their positions
            var allItems = await _dbContext.UpgradeStates.ToListAsync(cancellationToken);

            // Shift existing positions
            foreach (var item in allItems)
            {
                item.QueuePosition += items.Count;
            }

            // Add new items at the front
            int position = 0;
            foreach (var (itemType, itemId, parentSeriesId, seasonNumber, episodeNumber) in items)
            {
                var existingItem = await _dbContext.UpgradeStates.FirstOrDefaultAsync(u => u.ItemId == itemId && u.ItemType == itemType, cancellationToken);

                if (existingItem is not null)
                {
                    // Reset state for existing item
                    existingItem.SearchState = SearchState.Pending;
                    existingItem.IsMonitored = true;
                    existingItem.QueuePosition = position++;
                }
                else
                {
                    // Create new queue entry
                    var newItem = new UpgradeState
                    {
                        ItemId = itemId,
                        ItemType = itemType,
                        ParentSeriesId = parentSeriesId,
                        SeasonNumber = seasonNumber,
                        EpisodeNumber = episodeNumber,
                        Title = $"{itemType} {itemId}",
                        SearchState = SearchState.Pending,
                        IsMonitored = true,
                        QueuePosition = position++,
                        CreatedAt = _timeProvider.GetUtcNow(),
                    };
                    _dbContext.UpgradeStates.Add(newItem);
                }
            }

            await _dbContext.SaveChangesAsync(cancellationToken);
            _logger.LogAddedItemsToFrontOfQueue(items.Count);
        }
        catch (Exception ex)
        {
            _logger.LogErrorAddingItemsToQueue(ex);
        }
    }

    private async Task ExecuteSeriesSearchAsync(int seriesId, CancellationToken cancellationToken = default)
    {
        try
        {
            await _sonarrClient.SearchSeriesAsync(seriesId, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogErrorExecutingSeriesSearch(ex, seriesId);
        }
    }

    private async Task ExecuteSeasonSearchAsync(int seriesId, int seasonNumber, CancellationToken cancellationToken = default)
    {
        try
        {
            await _sonarrClient.SearchSeasonAsync(seriesId, seasonNumber, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogErrorExecutingSeasonSearch(ex, seriesId, seasonNumber);
        }
    }

    private async Task ExecuteEpisodeSearchAsync(IList<int> episodeIds, CancellationToken cancellationToken = default)
    {
        try
        {
            await _sonarrClient.SearchEpisodesAsync(episodeIds, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogErrorExecutingEpisodeSearch(ex);
        }
    }

    private async Task ExecuteMovieSearchAsync(IList<int> movieIds, CancellationToken cancellationToken = default)
    {
        try
        {
            await _radarrClient.SearchMoviesAsync(movieIds, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogErrorExecutingMovieSearch(ex);
        }
    }
}
