using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.Extensions.Logging;
using Upgradarr.Apps.Models;
using Upgradarr.Apps.Sonarr.Models;
using Upgradarr.Domain.Entities;
using Upgradarr.Domain.Enums;
using Upgradarr.Domain.Interfaces;
using Upgradarr.Domain.ValueObjects;

namespace Upgradarr.Apps.Sonarr;

public class SonarrClient : IQueueManager
{
    private readonly HttpClient _client;
    private readonly ILogger<SonarrClient> _logger;

    public SonarrClient(HttpClient client, ILogger<SonarrClient> logger)
    {
        _client = client;
        _logger = logger;
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
