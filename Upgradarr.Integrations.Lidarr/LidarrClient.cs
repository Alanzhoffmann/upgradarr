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
using Upgradarr.Integrations.Interfaces;
using Upgradarr.Integrations.Lidarr.Extensions;
using Upgradarr.Integrations.Lidarr.Models;
using Upgradarr.Integrations.Models;

namespace Upgradarr.Integrations.Lidarr;

public class LidarrClient : QueueManagerBase<LidarrQueueResource>, IQueueManager, IUpgradeManager
{
    private readonly HttpClient _client;
    private readonly TimeProvider _timeProvider;
    private readonly HybridCache _hybridCache;

    public LidarrClient(HttpClient client, ILogger<LidarrClient> logger, TimeProvider timeProvider, HybridCache hybridCache)
        : base(logger)
    {
        _client = client;
        _timeProvider = timeProvider;
        _hybridCache = hybridCache;
    }

    public RecordSource SourceName => RecordSource.Lidarr;

    public bool CanHandle(ItemType itemType) => itemType is ItemType.Artist or ItemType.Album;

    public async IAsyncEnumerable<UpgradeState> BuildQueueItemsAsync([EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var artists = await GetArtistsAsync(cancellationToken);

        foreach (var artist in artists.Where(a => a.Monitored))
        {
            var items = await BuildArtistQueueItemsAsync(artist, cancellationToken);
            foreach (var item in items)
            {
                yield return item;
            }
        }
    }

    public async IAsyncEnumerable<UpgradeState> GetNewQueueItemsAsync(
        HashSet<int> existingIds,
        [EnumeratorCancellation] CancellationToken cancellationToken = default
    )
    {
        var artists = await GetArtistsAsync(cancellationToken);

        foreach (var artist in artists.Where(a => a.Monitored && !existingIds.Contains(a.Id)))
        {
            var items = await BuildArtistQueueItemsAsync(artist, cancellationToken);
            foreach (var item in items)
            {
                yield return item;
            }
        }
    }

    private async Task<List<UpgradeState>> BuildArtistQueueItemsAsync(ArtistResource artist, CancellationToken cancellationToken)
    {
        var queueItems = new List<UpgradeState>();

        var artistItem = new UpgradeState
        {
            ItemId = artist.Id,
            Title = artist.ArtistName,
            ItemType = ItemType.Artist,
            SearchState = SearchState.Pending,
            IsMonitored = true,
            CreatedAt = _timeProvider.GetUtcNow(),
        };
        queueItems.Add(artistItem);

        var albums = await GetAlbumsAsync(artistId: artist.Id, cancellationToken: cancellationToken);

        foreach (var album in albums.Where(a => a.Monitored))
        {
            bool isMissing = album.Statistics?.TrackFileCount == 0 && album.Statistics?.TotalTrackCount > 0;

            var albumItem = new UpgradeState
            {
                ItemId = album.Id,
                ParentSeriesId = artist.Id,
                Title = $"{artist.ArtistName} - {album.Title}",
                ItemType = ItemType.Album,
                SearchState = SearchState.Pending,
                IsMonitored = true,
                IsMissing = isMissing,
                ReleaseDate = album.ReleaseDate,
                CreatedAt = _timeProvider.GetUtcNow(),
            };
            queueItems.Add(albumItem);

            if (isMissing)
            {
                artistItem.IsMissing = true;
            }
        }

        return queueItems;
    }

    public async Task<UpgradeActionResult> ProcessUpgradeAsync(UpgradeState state, CancellationToken cancellationToken = default)
    {
        return state.ItemType switch
        {
            ItemType.Artist => await ProcessArtistUpgradeAsync(state, cancellationToken),
            ItemType.Album => await ProcessAlbumUpgradeAsync(state, cancellationToken),
            _ => UpgradeActionResult.Skipped,
        };
    }

    private async Task<UpgradeActionResult> ProcessArtistUpgradeAsync(UpgradeState state, CancellationToken cancellationToken)
    {
        try
        {
            var artist = await GetArtistByIdAsync(state.ItemId, cancellationToken);
            if (artist is null)
            {
                _logger.LogArtistNotFound(state.ItemId);
                return UpgradeActionResult.Searched;
            }

            if (!artist.Monitored)
            {
                _logger.LogArtistNoLongerMonitored(artist.ArtistName ?? "Unknown", state.ItemId);
                return UpgradeActionResult.Removed;
            }

            _logger.LogSearchingForArtist(artist.ArtistName ?? "Unknown");
            await SearchArtistAsync(state.ItemId, cancellationToken);
            return UpgradeActionResult.Searched;
        }
        catch (Exception ex)
        {
            _logger.LogErrorProcessingArtistUpgrade(ex, state.ItemId);
            return UpgradeActionResult.Skipped;
        }
    }

    private async Task<UpgradeActionResult> ProcessAlbumUpgradeAsync(UpgradeState state, CancellationToken cancellationToken)
    {
        try
        {
            var album = await GetAlbumByIdAsync(state.ItemId, cancellationToken);
            if (album is null)
            {
                _logger.LogAlbumNoLongerMonitored(state.Title ?? "Unknown", state.ItemId);
                return UpgradeActionResult.Searched;
            }

            if (!album.Monitored)
            {
                _logger.LogAlbumNoLongerMonitored(album.Title ?? "Unknown", state.ItemId);
                return UpgradeActionResult.Removed;
            }

            if (await HasAlbumOngoingDownloadAsync(state, cancellationToken))
            {
                return UpgradeActionResult.Skipped;
            }

            _logger.LogSearchingForAlbum(album.Title ?? "Unknown");
            await SearchAlbumsAsync([state.ItemId], cancellationToken);
            return UpgradeActionResult.Searched;
        }
        catch (Exception ex)
        {
            _logger.LogErrorProcessingAlbumUpgrade(ex, state.ItemId);
            return UpgradeActionResult.Skipped;
        }
    }

    public async Task<bool> HasOngoingDownloadAsync(UpgradeState state, CancellationToken cancellationToken = default)
    {
        return state.ItemType switch
        {
            ItemType.Artist => await HasArtistOngoingDownloadAsync(state, cancellationToken),
            ItemType.Album => await HasAlbumOngoingDownloadAsync(state, cancellationToken),
            _ => false,
        };
    }

    private async Task<bool> HasArtistOngoingDownloadAsync(UpgradeState state, CancellationToken cancellationToken)
    {
        var queue = GetAllQueueItems(cancellationToken);
        if (await queue.AnyAsync(q => q is LidarrQueueResource lidarrResource && lidarrResource.ArtistId == state.ItemId, cancellationToken: cancellationToken))
        {
            _logger.LogArtistHasOngoingDownloads(state.Title ?? "Unknown", state.ItemId);
            return true;
        }
        return false;
    }

    private async Task<bool> HasAlbumOngoingDownloadAsync(UpgradeState state, CancellationToken cancellationToken)
    {
        var queue = GetAllQueueItems(cancellationToken);
        if (await queue.AnyAsync(q => q is LidarrQueueResource lidarrResource && lidarrResource.AlbumId == state.ItemId, cancellationToken: cancellationToken))
        {
            _logger.LogAlbumIsDownloading(state.Title ?? "Unknown", state.ItemId);
            return true;
        }
        return false;
    }

    public async Task<IList<ArtistResource>> GetArtistsAsync(CancellationToken cancellationToken = default) =>
        await _hybridCache.GetOrCreateAsync(
            "lidarr_artists",
            async ct => await _client.GetFromJsonAsync("/api/v1/artist", LidarrClientJsonSerializerContext.Default.IListArtistResource, ct) ?? [],
            options: null,
            tags: ["lidarr", "lidarr_artists"],
            cancellationToken: cancellationToken
        );

    public async Task<ArtistResource?> GetArtistByIdAsync(int id, CancellationToken cancellationToken = default) =>
        await _hybridCache.GetOrCreateAsync(
            $"lidarr_artist_{id}",
            async ct => await _client.GetFromJsonAsync($"/api/v1/artist/{id}", LidarrClientJsonSerializerContext.Default.ArtistResource, ct),
            options: null,
            tags: ["lidarr", "lidarr_artists", $"lidarr_artist_{id}"],
            cancellationToken: cancellationToken
        );

    public async Task<IList<AlbumResource>> GetAlbumsAsync(
        int? artistId = null,
        IList<int>? albumIds = null,
        string? foreignAlbumId = null,
        bool includeAllArtistAlbums = false,
        CancellationToken cancellationToken = default
    )
    {
        var queryBuilder = new QueryBuilder();
        if (artistId.HasValue)
            queryBuilder.Add("artistId", artistId.Value.ToString());
        if (foreignAlbumId != null)
            queryBuilder.Add("foreignAlbumId", foreignAlbumId);
        if (includeAllArtistAlbums)
            queryBuilder.Add("includeAllArtistAlbums", "true");
        if (albumIds?.Count > 0)
            foreach (var id in albumIds)
                queryBuilder.Add("albumIds", id.ToString());

        return await _client.GetFromJsonAsync(
                $"/api/v1/album{queryBuilder.ToQueryString()}",
                LidarrClientJsonSerializerContext.Default.IListAlbumResource,
                cancellationToken
            ) ?? [];
    }

    public async Task<AlbumResource?> GetAlbumByIdAsync(int id, CancellationToken cancellationToken = default) =>
        await _hybridCache.GetOrCreateAsync(
            $"lidarr_album_{id}",
            async ct => await _client.GetFromJsonAsync($"/api/v1/album/{id}", LidarrClientJsonSerializerContext.Default.AlbumResource, ct),
            options: null,
            tags: ["lidarr", "lidarr_albums", $"lidarr_album_{id}"],
            cancellationToken: cancellationToken
        );

    public async Task<PagingResource<LidarrQueueResource>> GetQueueAsync(
        int page = 1,
        int pageSize = 10,
        string? sortKey = null,
        string? sortDirection = null,
        bool includeArtist = false,
        bool includeAlbum = false,
        CancellationToken cancellationToken = default
    )
    {
        var queryBuilder = new QueryBuilder { { "page", page.ToString() }, { "pageSize", pageSize.ToString() } };
        if (!string.IsNullOrEmpty(sortKey))
            queryBuilder.Add("sortKey", sortKey);
        if (!string.IsNullOrEmpty(sortDirection))
            queryBuilder.Add("sortDirection", sortDirection);
        if (includeArtist)
            queryBuilder.Add("includeArtist", "true");
        if (includeAlbum)
            queryBuilder.Add("includeAlbum", "true");

        return await _client.GetFromJsonAsync(
                $"/api/v1/queue{queryBuilder.ToQueryString()}",
                LidarrClientJsonSerializerContext.Default.PagingResourceLidarrQueueResource,
                cancellationToken
            ) ?? new();
    }

    public async Task<CommandResource?> SearchArtistAsync(int artistId, CancellationToken cancellationToken = default)
    {
        var command = new ArtistSearchCommand { ArtistId = artistId };
        var response = await _client.PostAsJsonAsync(
            "/api/v1/command",
            command,
            LidarrClientJsonSerializerContext.Default.ArtistSearchCommand,
            cancellationToken
        );
        return await response.Content.ReadFromJsonAsync(LidarrClientJsonSerializerContext.Default.CommandResource, cancellationToken);
    }

    public async Task<CommandResource?> SearchAlbumsAsync(IList<int> albumIds, CancellationToken cancellationToken = default)
    {
        var command = new AlbumSearchCommand { AlbumIds = albumIds };
        var response = await _client.PostAsJsonAsync(
            "/api/v1/command",
            command,
            LidarrClientJsonSerializerContext.Default.AlbumSearchCommand,
            cancellationToken
        );
        return await response.Content.ReadFromJsonAsync(LidarrClientJsonSerializerContext.Default.CommandResource, cancellationToken);
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
            var response = await _client.DeleteAsync($"/api/v1/queue/{itemId}{queryBuilder.ToQueryString()}", cancellationToken);
            response.EnsureSuccessStatusCode();
            await _hybridCache.RemoveByTagAsync("lidarr", cancellationToken);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogErrorDeletingQueueItem(ex, itemId);
            return false;
        }
    }

    protected override Task<IEnumerable<ItemToQueue>> GetRequeueItemsAsync(int itemId, CancellationToken cancellationToken)
    {
        return Task.FromResult<IEnumerable<ItemToQueue>>([new(ItemType.Album, itemId)]);
    }

    protected override Task<PagingResource<LidarrQueueResource>> GetQueuePageAsync(int page, int pageSize, CancellationToken cancellationToken)
    {
        return GetQueueAsync(page, pageSize, includeArtist: true, includeAlbum: true, cancellationToken: cancellationToken);
    }

    protected override async Task<IQueueResource> ProcessQueueItemForYieldAsync(LidarrQueueResource item, CancellationToken cancellationToken)
    {
        if (item.DownloadId != null && item.DownloadId.Equals(item.Title, StringComparison.OrdinalIgnoreCase) && item.AlbumId.HasValue)
        {
            var album = await GetAlbumByIdAsync(item.AlbumId.Value, cancellationToken);
            if (album is not null)
            {
                var artistName = album.Artist?.ArtistName ?? item.Artist?.ArtistName;
                return item with { Title = artistName != null ? $"{artistName} - {album.Title}" : album.Title };
            }
        }
        return item;
    }

    public async ValueTask<(bool ShouldRemove, int DownloadedScore)> ShouldRemoveImmediately(IQueueResource item, CancellationToken cancellationToken = default)
    {
        if (item is not LidarrQueueResource lidarrItem || !lidarrItem.AlbumId.HasValue)
        {
            return (false, 0);
        }

        var album = await GetAlbumByIdAsync(lidarrItem.AlbumId.Value, cancellationToken);
        if (album?.Statistics is null || album.Statistics.TrackFileCount == 0)
        {
            return (false, 0);
        }

        // Album has downloaded files, compare scores
        var shouldRemove = item.CustomFormatScore <= 0;
        return (shouldRemove, 0);
    }
}

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, UseStringEnumConverter = true)]
[JsonSerializable(typeof(IList<ArtistResource>))]
[JsonSerializable(typeof(ArtistResource))]
[JsonSerializable(typeof(IList<AlbumResource>))]
[JsonSerializable(typeof(AlbumResource))]
[JsonSerializable(typeof(PagingResource<LidarrQueueResource>))]
[JsonSerializable(typeof(CommandResource))]
[JsonSerializable(typeof(ArtistSearchCommand))]
[JsonSerializable(typeof(AlbumSearchCommand))]
internal partial class LidarrClientJsonSerializerContext : JsonSerializerContext;
