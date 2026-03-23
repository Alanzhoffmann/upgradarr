using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Upgradarr.Apps.Models;
using Upgradarr.Apps.Sonarr.Models;
using Upgradarr.Apps.Sonarr.Options;

namespace Huntarr.Net.Clients;

public class SonarrClient
{
    private readonly HttpClient _client;
    private readonly ILogger<SonarrClient> _logger;

    public SonarrClient(HttpClient client, IOptionsSnapshot<SonarrOptions> options, ILogger<SonarrClient> logger)
    {
        _client = client;
        _client.BaseAddress = new Uri(options.Value.BaseUrl);
        if (!string.IsNullOrEmpty(options.Value.ApiKey))
        {
            _client.DefaultRequestHeaders.Add("X-Api-Key", options.Value.ApiKey);
        }
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

    public async Task<PagingResource<SonarrQueueResource>> GetQueueAsync(CancellationToken cancellationToken = default) =>
        await _client.GetFromJsonAsync("/api/v3/queue", SonarrClientJsonSerializerContext.Default.PagingResourceSonarrQueueResource, cancellationToken)
        ?? new();

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
