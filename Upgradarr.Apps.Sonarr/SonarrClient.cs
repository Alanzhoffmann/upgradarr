using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.Extensions.Logging;
using Upgradarr.Apps.Models;
using Upgradarr.Apps.Sonarr.Extensions;
using Upgradarr.Apps.Sonarr.Models;
using Upgradarr.Domain.Entities;
using Upgradarr.Domain.Enums;
using Upgradarr.Domain.Interfaces;
using Upgradarr.Domain.ValueObjects;

namespace Upgradarr.Apps.Sonarr;

public class SonarrClient : IQueueManager, IUpgradeManager
{
    private readonly HttpClient _client;
    private readonly ILogger<SonarrClient> _logger;
    private readonly TimeProvider _timeProvider;

    public SonarrClient(HttpClient client, ILogger<SonarrClient> logger, TimeProvider timeProvider)
    {
        _client = client;
        _logger = logger;
        _timeProvider = timeProvider;
    }

    public RecordSource SourceName => RecordSource.Sonarr;

    public bool CanHandle(ItemType itemType) => itemType is ItemType.Series or ItemType.Season or ItemType.Episode;

    public async Task<List<UpgradeState>> BuildQueueItemsAsync(CancellationToken cancellationToken = default)
    {
        var series = await GetSeriesAsync(cancellationToken);
        var queueItems = new List<UpgradeState>();

        // Create entries for all monitored series
        foreach (var s in series.Where(s => s.Monitored))
        {
            await ProcessSeriesIntoQueueAsync(s, queueItems, cancellationToken);
        }

        return queueItems;
    }

    public async Task<List<UpgradeState>> GetNewQueueItemsAsync(HashSet<int> existingIds, CancellationToken cancellationToken = default)
    {
        var series = await GetSeriesAsync(cancellationToken);
        var queueItems = new List<UpgradeState>();

        // Create entries for all monitored series that don't exist yet
        foreach (var s in series.Where(s => s.Monitored && !existingIds.Contains(s.Id)))
        {
            await ProcessSeriesIntoQueueAsync(s, queueItems, cancellationToken);
        }

        return queueItems;
    }

    private async Task ProcessSeriesIntoQueueAsync(SeriesResource s, List<UpgradeState> queueItems, CancellationToken cancellationToken)
    {
        if (s.FirstAired.HasValue && s.FirstAired.Value > _timeProvider.GetUtcNow())
            return;

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

        var episodes = await GetEpisodesAsync(seriesId: s.Id, cancellationToken: cancellationToken);

        var seasonGroups = episodes.Where(e => e.Monitored).GroupBy(e => e.SeasonNumber).OrderBy(g => g.Key).ToList();

        foreach (var seasonGroup in seasonGroups)
        {
            int seasonNumber = seasonGroup.Key;
            var seasonItem = new UpgradeState
            {
                ItemId = seasonNumber,
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

            foreach (var episode in seasonGroup.OrderBy(e => e.EpisodeNumber))
            {
                if (episode.AirDateUtc.HasValue && episode.AirDateUtc.Value > _timeProvider.GetUtcNow())
                    continue;

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

                if (isMissing)
                {
                    seasonItem.IsMissing = true;
                    seriesItem.IsMissing = true;
                }
            }
        }
    }

    public async Task<UpgradeActionResult> ProcessUpgradeAsync(UpgradeState state, CancellationToken cancellationToken = default)
    {
        return state.ItemType switch
        {
            ItemType.Series => await ProcessSeriesUpgradeAsync(state, cancellationToken),
            ItemType.Season => await ProcessSeasonUpgradeAsync(state, cancellationToken),
            ItemType.Episode => await ProcessEpisodeUpgradeAsync(state, cancellationToken),
            _ => UpgradeActionResult.Skipped,
        };
    }

    private async Task<UpgradeActionResult> ProcessSeriesUpgradeAsync(UpgradeState state, CancellationToken cancellationToken)
    {
        try
        {
            var series = await GetSeriesByIdAsync(state.ItemId, cancellationToken: cancellationToken);
            if (series is null)
            {
                _logger.LogSeriesNotFound(state.ItemId);
                return UpgradeActionResult.Searched;
            }

            if (!series.Monitored)
            {
                _logger.LogSeriesNoLongerMonitored(series.Title ?? "Unknown", state.ItemId);
                return UpgradeActionResult.Removed;
            }

            _logger.LogSearchingForSeries(series.Title ?? "Unknown");
            await SearchSeriesAsync(state.ItemId, cancellationToken);
            return UpgradeActionResult.Searched;
        }
        catch (Exception ex)
        {
            _logger.LogErrorProcessingSeriesUpgrade(ex, state.ItemId);
            return UpgradeActionResult.Skipped;
        }
    }

    private async Task<UpgradeActionResult> ProcessSeasonUpgradeAsync(UpgradeState state, CancellationToken cancellationToken)
    {
        try
        {
            if (!state.ParentSeriesId.HasValue || !state.SeasonNumber.HasValue)
                return UpgradeActionResult.Skipped;

            var series = await GetSeriesByIdAsync(state.ParentSeriesId.Value, cancellationToken: cancellationToken);
            if (series is null)
                return UpgradeActionResult.Skipped;

            if (!series.Monitored)
            {
                _logger.LogSeriesNoLongerMonitored(series.Title ?? "Unknown", state.ParentSeriesId.Value);
                return UpgradeActionResult.Removed;
            }

            if (await HasSeasonOngoingDownloadAsync(state, cancellationToken))
            {
                return UpgradeActionResult.Skipped;
            }

            _logger.LogSearchingForSeason(series.Title ?? "Unknown", state.SeasonNumber.Value);
            await SearchSeasonAsync(state.ParentSeriesId.Value, state.SeasonNumber.Value, cancellationToken);
            return UpgradeActionResult.Searched;
        }
        catch (Exception ex)
        {
            _logger.LogErrorProcessingSeasonUpgrade(ex, state.ParentSeriesId ?? 0, state.SeasonNumber ?? 0);
            return UpgradeActionResult.Skipped;
        }
    }

    private async Task<UpgradeActionResult> ProcessEpisodeUpgradeAsync(UpgradeState state, CancellationToken cancellationToken)
    {
        try
        {
            if (!state.ParentSeriesId.HasValue || !state.SeasonNumber.HasValue || !state.EpisodeNumber.HasValue)
                return UpgradeActionResult.Skipped;

            var series = await GetSeriesByIdAsync(state.ParentSeriesId.Value, cancellationToken: cancellationToken);
            if (series is null)
                return UpgradeActionResult.Skipped;

            if (!series.Monitored)
            {
                _logger.LogSeriesNoLongerMonitored(series.Title ?? "Unknown", state.ParentSeriesId.Value);
                return UpgradeActionResult.Removed;
            }

            var episodes = await GetEpisodesAsync(
                seriesId: state.ParentSeriesId.Value,
                seasonNumber: state.SeasonNumber.Value,
                cancellationToken: cancellationToken
            );
            var episode = episodes.FirstOrDefault(e => e.SeasonNumber == state.SeasonNumber && e.EpisodeNumber == state.EpisodeNumber);

            if (episode is null || !episode.Monitored)
            {
                _logger.LogEpisodeNotFoundOrUnmonitored(series.Title ?? "Unknown", state.SeasonNumber.Value, state.EpisodeNumber.Value, state.ItemId);
                return UpgradeActionResult.Removed;
            }

            if (await HasEpisodeOngoingDownloadAsync(state, cancellationToken))
            {
                return UpgradeActionResult.Skipped;
            }

            _logger.LogSearchingForEpisode(
                series.Title ?? "Unknown",
                state.SeasonNumber.Value.ToString().PadLeft(2, '0'),
                state.EpisodeNumber.Value.ToString().PadLeft(2, '0')
            );
            await SearchEpisodesAsync([episode.Id], cancellationToken);
            return UpgradeActionResult.Searched;
        }
        catch (Exception ex)
        {
            _logger.LogErrorProcessingEpisodeUpgrade(ex, state.ItemId);
            return UpgradeActionResult.Skipped;
        }
    }

    public async Task<bool> HasOngoingDownloadAsync(UpgradeState state, CancellationToken cancellationToken = default)
    {
        return state.ItemType switch
        {
            ItemType.Series => await HasSeriesOngoingDownloadAsync(state, cancellationToken),
            ItemType.Season => await HasSeasonOngoingDownloadAsync(state, cancellationToken),
            ItemType.Episode => await HasEpisodeOngoingDownloadAsync(state, cancellationToken),
            _ => false,
        };
    }

    private async Task<bool> HasSeriesOngoingDownloadAsync(UpgradeState state, CancellationToken cancellationToken)
    {
        var queue = GetAllQueueItems(cancellationToken);
        if (await queue.AnyAsync(q => q is SonarrQueueResource sonarrResource && sonarrResource.SeriesId == state.ItemId, cancellationToken: cancellationToken))
        {
            _logger.LogSeriesHasOngoingDownloads(state.Title ?? "Unknown", state.ItemId);
            return true;
        }
        return false;
    }

    private async Task<bool> HasSeasonOngoingDownloadAsync(UpgradeState state, CancellationToken cancellationToken)
    {
        if (!state.ParentSeriesId.HasValue || !state.SeasonNumber.HasValue)
            return false;

        var queue = GetAllQueueItems(cancellationToken);
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

    private async Task<bool> HasEpisodeOngoingDownloadAsync(UpgradeState state, CancellationToken cancellationToken)
    {
        if (!state.ParentSeriesId.HasValue || !state.SeasonNumber.HasValue || !state.EpisodeNumber.HasValue)
            return false;

        var queue = GetAllQueueItems(cancellationToken);
        if (
            await queue.AnyAsync(q => q is SonarrQueueResource sonarrResource && sonarrResource.EpisodeId == state.ItemId, cancellationToken: cancellationToken)
        )
        {
            _logger.LogEpisodeIsDownloading(state.Title ?? "Unknown", state.SeasonNumber.Value, state.EpisodeNumber.Value, state.ItemId);
            return true;
        }
        return false;
    }

    public async Task<IList<SeriesResource>> GetSeriesAsync(CancellationToken cancellationToken = default) =>
        await _client.GetFromJsonAsync("/api/v3/series", SonarrClientJsonSerializerContext.Default.IListSeriesResource, cancellationToken) ?? [];

    public async Task<SeriesResource?> GetSeriesByIdAsync(int id, bool includeSeasonImages = false, CancellationToken cancellationToken = default)
    {
        var queryBuilder = new QueryBuilder { { "includeSeasonImages", includeSeasonImages.ToString().ToLower() } };
        return await _client.GetFromJsonAsync(
            $"/api/v3/series/{id}{queryBuilder.ToQueryString()}",
            SonarrClientJsonSerializerContext.Default.SeriesResource,
            cancellationToken
        );
    }

    public async Task<IList<EpisodeResource>> GetEpisodesAsync(
        int? seriesId = null,
        int? seasonNumber = null,
        IList<int>? episodeIds = null,
        bool includeSeries = false,
        bool includeEpisodeFile = false,
        bool includeImages = false,
        CancellationToken cancellationToken = default
    )
    {
        var queryBuilder = new QueryBuilder();
        if (seriesId.HasValue)
            queryBuilder.Add("seriesId", seriesId.Value.ToString());
        if (seasonNumber.HasValue)
            queryBuilder.Add("seasonNumber", seasonNumber.Value.ToString());
        if (includeSeries)
            queryBuilder.Add("includeSeries", "true");
        if (includeEpisodeFile)
            queryBuilder.Add("includeEpisodeFile", "true");
        if (includeImages)
            queryBuilder.Add("includeImages", "true");

        if (episodeIds?.Count > 0)
            foreach (var id in episodeIds)
                queryBuilder.Add("episodeIds", id.ToString());

        return await _client.GetFromJsonAsync(
                $"/api/v3/episode{queryBuilder.ToQueryString()}",
                SonarrClientJsonSerializerContext.Default.IListEpisodeResource,
                cancellationToken
            ) ?? [];
    }

    public async Task<EpisodeResource?> GetEpisodeByIdAsync(int id, CancellationToken cancellationToken = default) =>
        await _client.GetFromJsonAsync($"/api/v3/episode/{id}", SonarrClientJsonSerializerContext.Default.EpisodeResource, cancellationToken);

    public async Task<PagingResource<SonarrQueueResource>> GetQueueAsync(
        int page = 1,
        int pageSize = 10,
        string? sortKey = null,
        string? sortDirection = null,
        bool includeSeries = false,
        bool includeEpisode = false,
        CancellationToken cancellationToken = default
    )
    {
        var queryBuilder = new QueryBuilder { { "page", page.ToString() }, { "pageSize", pageSize.ToString() } };
        if (!string.IsNullOrEmpty(sortKey))
            queryBuilder.Add("sortKey", sortKey);
        if (!string.IsNullOrEmpty(sortDirection))
            queryBuilder.Add("sortDirection", sortDirection);
        if (includeSeries)
            queryBuilder.Add("includeSeries", "true");
        if (includeEpisode)
            queryBuilder.Add("includeEpisode", "true");

        return await _client.GetFromJsonAsync(
                $"/api/v3/queue{queryBuilder.ToQueryString()}",
                SonarrClientJsonSerializerContext.Default.PagingResourceSonarrQueueResource,
                cancellationToken
            ) ?? new();
    }

    public async Task<bool> DeleteQueueItemAsync(
        int itemId,
        bool removeFromClient = true,
        bool blocklist = false,
        bool skipRedownload = true,
        CancellationToken cancellationToken = default
    )
    {
        var queryBuilder = new QueryBuilder
        {
            { "removeFromClient", removeFromClient.ToString().ToLower() },
            { "blocklist", blocklist.ToString().ToLower() },
            { "skipRedownload", skipRedownload.ToString().ToLower() },
        };
        try
        {
            var response = await _client.DeleteAsync($"/api/v3/queue/{itemId}{queryBuilder.ToQueryString()}", cancellationToken);
            response.EnsureSuccessStatusCode();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting queue item {ItemId}", itemId);
            return false;
        }
        return true;
    }

    public async Task<CommandResource?> SearchSeriesAsync(int seriesId, CancellationToken cancellationToken = default)
    {
        var command = new SeriesSearchCommand { SeriesId = seriesId };
        var response = await _client.PostAsJsonAsync(
            "/api/v3/command",
            command,
            SonarrClientJsonSerializerContext.Default.SeriesSearchCommand,
            cancellationToken
        );
        return await response.Content.ReadFromJsonAsync(SonarrClientJsonSerializerContext.Default.CommandResource, cancellationToken);
    }

    public async Task<CommandResource?> SearchSeasonAsync(int seriesId, int seasonNumber, CancellationToken cancellationToken = default)
    {
        var command = new SeasonSearchCommand { SeriesId = seriesId, SeasonNumber = seasonNumber };
        var response = await _client.PostAsJsonAsync(
            "/api/v3/command",
            command,
            SonarrClientJsonSerializerContext.Default.SeasonSearchCommand,
            cancellationToken
        );
        return await response.Content.ReadFromJsonAsync(SonarrClientJsonSerializerContext.Default.CommandResource, cancellationToken);
    }

    public async Task<CommandResource?> SearchEpisodesAsync(IList<int> episodeIds, CancellationToken cancellationToken = default)
    {
        var command = new EpisodeSearchCommand { EpisodeIds = episodeIds };
        var response = await _client.PostAsJsonAsync(
            "/api/v3/command",
            command,
            SonarrClientJsonSerializerContext.Default.EpisodeSearchCommand,
            cancellationToken
        );
        return await response.Content.ReadFromJsonAsync(SonarrClientJsonSerializerContext.Default.CommandResource, cancellationToken);
    }

    public async ValueTask<(bool AllDeleted, List<ItemToQueue> ItemsToRequeue)> DeleteQueueItemsAsync(
        QueueRecord record,
        CancellationToken cancellationToken = default
    )
    {
        bool allDeleted = true;
        var itemsToRequeue = new List<ItemToQueue>();

        foreach (var itemScore in record.ItemScores)
        {
            if (
                !await DeleteQueueItemAsync(
                    itemScore.ItemId,
                    removeFromClient: true,
                    blocklist: true,
                    skipRedownload: true,
                    cancellationToken: cancellationToken
                )
            )
            {
                allDeleted = false;
            }
            else
            {
                // Get episode details to determine show/season/episode to re-queue
                try
                {
                    var episodes = await GetEpisodesAsync(episodeIds: [itemScore.ItemId], cancellationToken: cancellationToken);
                    var episode = episodes.FirstOrDefault();

                    if (episode is not null)
                    {
                        // Add show, season, and episode to re-queue
                        var seriesId = episode.SeriesId;

                        // Get series to add it back
                        var series = await GetSeriesByIdAsync(seriesId, cancellationToken: cancellationToken);
                        if (series is not null && series.Monitored)
                        {
                            itemsToRequeue.Add(new(ItemType.Series, seriesId));
                            itemsToRequeue.Add(new(ItemType.Season, episode.SeasonNumber, seriesId, episode.SeasonNumber));
                            itemsToRequeue.Add(new(ItemType.Episode, episode.Id, seriesId, episode.SeasonNumber, episode.EpisodeNumber));
                        }
                    }
                }
                catch
                {
                    // If we can't get episode details, just add the episode ID back
                    itemsToRequeue.Add(new(ItemType.Episode, itemScore.ItemId));
                }
            }
        }

        return (allDeleted, itemsToRequeue);
    }

    public async IAsyncEnumerable<IQueueResource> GetAllQueueItems([EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        const int PageSize = 100;

        var page = 1;
        PagingResource<SonarrQueueResource> items;
        do
        {
            items = await GetQueueAsync(page, PageSize, includeSeries: true, includeEpisode: true, cancellationToken: cancellationToken);
            foreach (var item in items.Records)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    yield break;
                }

                if (item.DownloadId is null)
                {
                    // Skip items with null DownloadId, as these cannot be tracked for cleanup
                    continue;
                }

                if (item.DownloadId.Equals(item.Title, StringComparison.OrdinalIgnoreCase) && item.EpisodeId.HasValue)
                {
                    var episode = await GetEpisodeByIdAsync(item.EpisodeId.Value, cancellationToken);
                    if (episode is not null)
                    {
                        yield return item with
                        {
                            Title = $"{episode.Series?.Title} {episode.SeasonNumber}x{episode.EpisodeNumber:00} - {episode.Title}",
                        };
                        continue;
                    }
                }

                yield return item;
            }

            page++;
        } while (items.Records?.Count > 0);
    }

    public async ValueTask<(bool ShouldRemove, int DownloadedScore)> ShouldRemoveImmediately(IQueueResource item, CancellationToken cancellationToken = default)
    {
        if (item is not SonarrQueueResource sonarrItem || !sonarrItem.EpisodeId.HasValue)
        {
            return (false, 0);
        }

        var episodes = await GetEpisodesAsync(episodeIds: [sonarrItem.EpisodeId.Value], includeEpisodeFile: true, cancellationToken: cancellationToken);

        var episode = episodes.FirstOrDefault();
        if (episode?.EpisodeFile is null)
        {
            return (false, 0);
        }

        // Episode has a downloaded file, compare scores
        var downloadedScore = episode.EpisodeFile.CustomFormatScore;
        var shouldRemove =
            item.CustomFormatScore <= downloadedScore
            && (
                sonarrItem.Quality?.Quality is null
                || episode.EpisodeFile.Quality?.Quality is null
                || sonarrItem.Quality.Quality.Resolution <= episode.EpisodeFile.Quality.Quality.Resolution
            );

        return (shouldRemove, downloadedScore);
    }
}

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, UseStringEnumConverter = true)]
[JsonSerializable(typeof(IList<SeriesResource>))]
[JsonSerializable(typeof(SeriesResource))]
[JsonSerializable(typeof(IList<EpisodeResource>))]
[JsonSerializable(typeof(EpisodeResource))]
[JsonSerializable(typeof(EpisodeFileResource))]
[JsonSerializable(typeof(PagingResource<SonarrQueueResource>))]
[JsonSerializable(typeof(CommandResource))]
[JsonSerializable(typeof(SeriesSearchCommand))]
[JsonSerializable(typeof(SeasonSearchCommand))]
[JsonSerializable(typeof(EpisodeSearchCommand))]
internal partial class SonarrClientJsonSerializerContext : JsonSerializerContext;
