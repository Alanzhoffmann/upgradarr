using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.Extensions.Logging;
using Upgradarr.Apps.Models;
using Upgradarr.Apps.Radarr.Models;
using Upgradarr.Domain.Entities;
using Upgradarr.Domain.Enums;
using Upgradarr.Domain.Interfaces;
using Upgradarr.Domain.ValueObjects;

namespace Upgradarr.Apps.Radarr;

public class RadarrClient : IQueueManager
{
    private readonly HttpClient _client;
    private readonly ILogger<RadarrClient> _logger;

    public RadarrClient(HttpClient client, ILogger<RadarrClient> logger)
    {
        _client = client;
        _logger = logger;
    }

    public async Task<IList<MovieResource>> GetMoviesAsync(
        int? tmdbId = null,
        bool excludeLocalCovers = false,
        int? languageId = null,
        CancellationToken cancellationToken = default
    )
    {
        var queryBuilder = new QueryBuilder();
        if (tmdbId.HasValue)
            queryBuilder.Add("tmdbId", tmdbId.Value.ToString());
        if (excludeLocalCovers)
            queryBuilder.Add("excludeLocalCovers", "true");
        if (languageId.HasValue)
            queryBuilder.Add("languageId", languageId.Value.ToString());

        return await _client.GetFromJsonAsync(
                $"/api/v3/movie{queryBuilder.ToQueryString()}",
                RadarrClientJsonSerializerContext.Default.IListMovieResource,
                cancellationToken
            ) ?? [];
    }

    public async Task<MovieResource?> GetMovieByIdAsync(int id, CancellationToken cancellationToken = default) =>
        await _client.GetFromJsonAsync($"/api/v3/movie/{id}", RadarrClientJsonSerializerContext.Default.MovieResource, cancellationToken);

    public async Task<PagingResource<RadarrQueueResource>> GetQueueAsync(
        int page = 1,
        int pageSize = 10,
        string? sortKey = null,
        string? sortDirection = null,
        bool includeMovie = false,
        CancellationToken cancellationToken = default
    )
    {
        var queryBuilder = new QueryBuilder { { "page", page.ToString() }, { "pageSize", pageSize.ToString() } };
        if (!string.IsNullOrEmpty(sortKey))
            queryBuilder.Add("sortKey", sortKey);
        if (!string.IsNullOrEmpty(sortDirection))
            queryBuilder.Add("sortDirection", sortDirection);
        if (includeMovie)
            queryBuilder.Add("includeMovie", "true");

        return await _client.GetFromJsonAsync(
                $"/api/v3/queue{queryBuilder.ToQueryString()}",
                RadarrClientJsonSerializerContext.Default.PagingResourceRadarrQueueResource,
                cancellationToken
            ) ?? new();
    }

    public async Task<bool> DeleteQueueItemAsync(
        int itemId,
        bool removeFromClient = true,
        bool blocklist = false,
        bool skipRedownload = false,
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
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting queue item {ItemId}", itemId);
            return false;
        }
    }

    public async Task<CommandResource?> SearchMoviesAsync(IList<int> movieIds, CancellationToken cancellationToken = default)
    {
        var command = new MoviesSearchCommand { MovieIds = movieIds };
        var response = await _client.PostAsJsonAsync(
            "/api/v3/command",
            command,
            RadarrClientJsonSerializerContext.Default.MoviesSearchCommand,
            cancellationToken
        );
        return await response.Content.ReadFromJsonAsync(RadarrClientJsonSerializerContext.Default.CommandResource, cancellationToken);
    }

    public async ValueTask<(bool AllDeleted, List<ItemToQueue> ItemsToRequeue)> DeleteQueueItemsAsync(
        QueueRecord record,
        CancellationToken cancellationToken = default
    )
    {
        var itemsToRequeue = new List<ItemToQueue>();
        var allDeleted = true;

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
                // Add movie to re-queue
                itemsToRequeue.Add(new(ItemType.Movie, itemScore.ItemId));
            }
        }

        return (allDeleted, itemsToRequeue);
    }

    public async IAsyncEnumerable<IQueueResource> GetAllQueueItems([EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        const int PageSize = 100;

        var page = 1;
        PagingResource<RadarrQueueResource> items;
        do
        {
            items = await GetQueueAsync(page, PageSize, cancellationToken: cancellationToken);
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

                if (item.DownloadId.Equals(item.Title, StringComparison.OrdinalIgnoreCase) && item.MovieId.HasValue)
                {
                    var movie = await GetMovieByIdAsync(item.MovieId.Value, cancellationToken);
                    if (movie is not null)
                    {
                        yield return item with
                        {
                            Title = movie.Title,
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
        if (item is not RadarrQueueResource radarrItem || !radarrItem.MovieId.HasValue)
        {
            return (false, 0);
        }

        var movie = await GetMovieByIdAsync(radarrItem.MovieId.Value, cancellationToken);

        if (movie?.MovieFile is null)
        {
            return (false, 0);
        }

        // Movie has a downloaded file, compare scores
        var downloadedScore = movie.MovieFile.CustomFormatScore ?? 0;
        var shouldRemove =
            item.CustomFormatScore <= downloadedScore
            && (
                radarrItem.Quality?.Quality is null
                || movie.MovieFile.Quality?.Quality is null
                || radarrItem.Quality.Quality.Resolution <= movie.MovieFile.Quality.Quality.Resolution
            );

        return (shouldRemove, downloadedScore);
    }
}

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, UseStringEnumConverter = true)]
[JsonSerializable(typeof(IList<MovieResource>))]
[JsonSerializable(typeof(MovieResource))]
[JsonSerializable(typeof(PagingResource<RadarrQueueResource>))]
[JsonSerializable(typeof(CommandResource))]
[JsonSerializable(typeof(MoviesSearchCommand))]
internal partial class RadarrClientJsonSerializerContext : JsonSerializerContext;
