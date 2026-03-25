using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Logging;
using Upgradarr.Domain.Entities;
using Upgradarr.Domain.Enums;
using Upgradarr.Domain.Interfaces;
using Upgradarr.Domain.ValueObjects;
using Upgradarr.Integrations.Models;
using Upgradarr.Integrations.Radarr.Extensions;
using Upgradarr.Integrations.Radarr.Models;

namespace Upgradarr.Integrations.Radarr;

public class RadarrClient : QueueManagerBase<RadarrQueueResource>, IQueueManager, IUpgradeManager
{
    private readonly HttpClient _client;
    private readonly TimeProvider _timeProvider;
    private readonly HybridCache _hybridCache;

    public RadarrClient(HttpClient client, ILogger<RadarrClient> logger, TimeProvider timeProvider, HybridCache hybridCache)
        : base(logger)
    {
        _client = client;
        _timeProvider = timeProvider;
        _hybridCache = hybridCache;
    }

    public RecordSource SourceName => RecordSource.Radarr;

    public bool CanHandle(ItemType itemType) => itemType == ItemType.Movie;

    public async IAsyncEnumerable<UpgradeState> BuildQueueItemsAsync([EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var movies = await GetMoviesAsync(cancellationToken: cancellationToken);

        foreach (var m in movies.Where(m => m.Monitored))
        {
            if (m.InCinemas.HasValue && m.InCinemas.Value > _timeProvider.GetUtcNow())
                continue;

            yield return new UpgradeState
            {
                ItemId = m.Id,
                Title = m.Title,
                ItemType = ItemType.Movie,
                SearchState = SearchState.Pending,
                IsMonitored = true,
                IsMissing = !m.HasFile,
                ReleaseDate = m.InCinemas,
                CreatedAt = _timeProvider.GetUtcNow(),
            };
        }
    }

    public async IAsyncEnumerable<UpgradeState> GetNewQueueItemsAsync(
        HashSet<int> existingIds,
        [EnumeratorCancellation] CancellationToken cancellationToken = default
    )
    {
        var movies = await GetMoviesAsync(cancellationToken: cancellationToken);

        foreach (var m in movies.Where(m => m.Monitored && !existingIds.Contains(m.Id)))
        {
            if (m.InCinemas.HasValue && m.InCinemas.Value > _timeProvider.GetUtcNow())
                continue;

            yield return new UpgradeState
            {
                ItemId = m.Id,
                Title = m.Title,
                ItemType = ItemType.Movie,
                SearchState = SearchState.Pending,
                IsMonitored = true,
                IsMissing = !m.HasFile,
                ReleaseDate = m.InCinemas,
                CreatedAt = _timeProvider.GetUtcNow(),
            };
        }
    }

    public async Task<UpgradeActionResult> ProcessUpgradeAsync(UpgradeState state, CancellationToken cancellationToken = default)
    {
        try
        {
            var movie = await GetMovieByIdAsync(state.ItemId, cancellationToken);
            if (movie is null)
            {
                _logger.LogMovieNotFound(state.ItemId);
                return UpgradeActionResult.Searched; // Treats as searched/completed so it doesn't loop forever
            }

            if (!movie.Monitored)
            {
                _logger.LogMovieNoLongerMonitored(movie.Title ?? "Unknown", state.ItemId);
                return UpgradeActionResult.Removed;
            }

            _logger.LogSearchingForMovie(movie.Title ?? "Unknown");
            await SearchMoviesAsync([state.ItemId], cancellationToken);
            return UpgradeActionResult.Searched;
        }
        catch (Exception ex)
        {
            _logger.LogErrorProcessingMovieUpgrade(ex, state.ItemId);
            return UpgradeActionResult.Skipped;
        }
    }

    public async Task<bool> HasOngoingDownloadAsync(UpgradeState state, CancellationToken cancellationToken = default)
    {
        var queue = GetAllQueueItems(cancellationToken);
        if (await queue.AnyAsync(q => q is RadarrQueueResource radarrResource && radarrResource.MovieId == state.ItemId, cancellationToken: cancellationToken))
        {
            _logger.LogMovieIsDownloading(state.Title ?? "Unknown", state.ItemId);
            return true;
        }

        return false;
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

        return await _hybridCache.GetOrCreateAsync(
            $"radarr_movies_{tmdbId}_{excludeLocalCovers}_{languageId}",
            async ct =>
                await _client.GetFromJsonAsync($"/api/v3/movie{queryBuilder.ToQueryString()}", RadarrClientJsonSerializerContext.Default.IListMovieResource, ct)
                ?? [],
            options: null,
            tags: ["radarr", "radarr_movies"],
            cancellationToken: cancellationToken
        );
    }

    public async Task<MovieResource?> GetMovieByIdAsync(int id, CancellationToken cancellationToken = default) =>
        await _hybridCache.GetOrCreateAsync(
            $"radarr_movie_{id}",
            async ct => await _client.GetFromJsonAsync($"/api/v3/movie/{id}", RadarrClientJsonSerializerContext.Default.MovieResource, ct),
            options: null,
            tags: ["radarr", "radarr_movies", $"radarr_movie_{id}"],
            cancellationToken: cancellationToken
        );

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

    protected override async Task<bool> DeleteQueueItemAsync(
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
            await _hybridCache.RemoveByTagAsync("radarr", cancellationToken);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogErrorDeletingQueueItem(ex, itemId);
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

    protected override Task<IEnumerable<ItemToQueue>> GetRequeueItemsAsync(int itemId, CancellationToken cancellationToken)
    {
        return Task.FromResult<IEnumerable<ItemToQueue>>([new(ItemType.Movie, itemId)]);
    }

    protected override Task<PagingResource<RadarrQueueResource>> GetQueuePageAsync(int page, int pageSize, CancellationToken cancellationToken)
    {
        return GetQueueAsync(page, pageSize, cancellationToken: cancellationToken);
    }

    protected override async Task<IQueueResource> ProcessQueueItemForYieldAsync(RadarrQueueResource item, CancellationToken cancellationToken)
    {
        if (item.DownloadId != null && item.DownloadId.Equals(item.Title, StringComparison.OrdinalIgnoreCase) && item.MovieId.HasValue)
        {
            var movie = await GetMovieByIdAsync(item.MovieId.Value, cancellationToken);
            if (movie is not null)
            {
                return item with { Title = movie.Title };
            }
        }
        return item;
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
