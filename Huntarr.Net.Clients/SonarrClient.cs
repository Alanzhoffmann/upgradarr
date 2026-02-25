using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Huntarr.Net.Clients.Models;
using Huntarr.Net.Clients.Options;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

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

    public async Task<IList<QualityProfileResource>> GetQualityProfilesAsync(CancellationToken cancellationToken = default) =>
        await _client.GetFromJsonAsync("/api/v3/qualityprofile", SonarrClientJsonSerializerContext.Default.IListQualityProfileResource, cancellationToken)
        ?? [];

    public async Task<SystemResource?> GetSystemInfoAsync(CancellationToken cancellationToken = default) =>
        await _client.GetFromJsonAsync("/api/v3/system/status", SonarrClientJsonSerializerContext.Default.SystemResource, cancellationToken);

    public async Task<PagingResource<EpisodeResource>> GetMissingEpisodesAsync(
        int page = 1,
        int pageSize = 10,
        bool includeSeries = false,
        bool includeImages = false,
        bool monitored = true,
        CancellationToken cancellationToken = default
    )
    {
        var queryBuilder = new QueryBuilder
        {
            { "page", page.ToString() },
            { "pageSize", pageSize.ToString() },
            { "includeSeries", includeSeries.ToString().ToLower() },
            { "includeImages", includeImages.ToString().ToLower() },
            { "monitored", monitored.ToString().ToLower() },
        };

        return await _client.GetFromJsonAsync(
                $"/api/v3/wanted/missing{queryBuilder.ToQueryString()}",
                SonarrClientJsonSerializerContext.Default.PagingResourceEpisodeResource,
                cancellationToken
            ) ?? new();
    }

    public async Task<IList<SeriesResource>> GetSeriesAsync(CancellationToken cancellationToken = default) =>
        await _client.GetFromJsonAsync("/api/v3/series", SonarrClientJsonSerializerContext.Default.IListSeriesResource, cancellationToken) ?? [];

    public async Task<PagingResource<SonarrQueueResource>> GetQueueAsync(CancellationToken cancellationToken = default) =>
        await _client.GetFromJsonAsync("/api/v3/queue", SonarrClientJsonSerializerContext.Default.PagingResourceSonarrQueueResource, cancellationToken)
        ?? new();

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

    /// <summary>
    /// Get all health check statuses
    /// </summary>
    public async Task<IList<HealthResource>> GetHealthAsync(CancellationToken cancellationToken = default) =>
        await _client.GetFromJsonAsync("/api/v3/health", SonarrClientJsonSerializerContext.Default.IListHealthResource, cancellationToken) ?? [];

    /// <summary>
    /// Search for series by term
    /// </summary>
    public async Task<IList<SeriesResource>> SearchSeriesAsync(string term, CancellationToken cancellationToken = default)
    {
        var queryBuilder = new QueryBuilder { { "term", term } };
        return await _client.GetFromJsonAsync(
                $"/api/v3/series/lookup{queryBuilder.ToQueryString()}",
                SonarrClientJsonSerializerContext.Default.IListSeriesResource,
                cancellationToken
            ) ?? [];
    }

    /// <summary>
    /// Get a specific series by ID
    /// </summary>
    public async Task<SeriesResource?> GetSeriesByIdAsync(int id, bool includeSeasonImages = false, CancellationToken cancellationToken = default)
    {
        var queryBuilder = new QueryBuilder { { "includeSeasonImages", includeSeasonImages.ToString().ToLower() } };
        return await _client.GetFromJsonAsync(
            $"/api/v3/series/{id}{queryBuilder.ToQueryString()}",
            SonarrClientJsonSerializerContext.Default.SeriesResource,
            cancellationToken
        );
    }

    /// <summary>
    /// Add a new series
    /// </summary>
    public async Task<SeriesResource?> AddSeriesAsync(SeriesResource series, CancellationToken cancellationToken = default)
    {
        var response = await _client.PostAsJsonAsync("/api/v3/series", series, SonarrClientJsonSerializerContext.Default.SeriesResource, cancellationToken);
        return await response.Content.ReadFromJsonAsync(SonarrClientJsonSerializerContext.Default.SeriesResource, cancellationToken);
    }

    /// <summary>
    /// Update an existing series
    /// </summary>
    public async Task<SeriesResource?> UpdateSeriesAsync(SeriesResource series, CancellationToken cancellationToken = default)
    {
        var response = await _client.PutAsJsonAsync(
            $"/api/v3/series/{series.Id}",
            series,
            SonarrClientJsonSerializerContext.Default.SeriesResource,
            cancellationToken
        );
        return await response.Content.ReadFromJsonAsync(SonarrClientJsonSerializerContext.Default.SeriesResource, cancellationToken);
    }

    /// <summary>
    /// Delete a series
    /// </summary>
    public async Task<bool> DeleteSeriesAsync(int id, bool deleteFiles = false, CancellationToken cancellationToken = default)
    {
        var queryBuilder = new QueryBuilder { { "deleteFiles", deleteFiles.ToString().ToLower() } };
        try
        {
            var response = await _client.DeleteAsync($"/api/v3/series/{id}{queryBuilder.ToQueryString()}", cancellationToken);
            response.EnsureSuccessStatusCode();
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting series {SeriesId}", id);
            return false;
        }
    }

    /// <summary>
    /// Get episodes by series ID or other filters
    /// </summary>
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

    /// <summary>
    /// Get a specific episode by ID
    /// </summary>
    public async Task<EpisodeResource?> GetEpisodeByIdAsync(int id, CancellationToken cancellationToken = default) =>
        await _client.GetFromJsonAsync($"/api/v3/episode/{id}", SonarrClientJsonSerializerContext.Default.EpisodeResource, cancellationToken);

    /// <summary>
    /// Update an episode
    /// </summary>
    public async Task<EpisodeResource?> UpdateEpisodeAsync(EpisodeResource episode, CancellationToken cancellationToken = default)
    {
        var response = await _client.PutAsJsonAsync(
            $"/api/v3/episode/{episode.Id}",
            episode,
            SonarrClientJsonSerializerContext.Default.EpisodeResource,
            cancellationToken
        );
        return await response.Content.ReadFromJsonAsync(SonarrClientJsonSerializerContext.Default.EpisodeResource, cancellationToken);
    }

    /// <summary>
    /// Set monitoring status for episodes
    /// </summary>
    public async Task<bool> SetEpisodeMonitoredAsync(IList<int> episodeIds, bool monitored, CancellationToken cancellationToken = default)
    {
        try
        {
            var request = new { episodeIds, monitored };
            var response = await _client.PutAsJsonAsync("/api/v3/episode/monitor", request, cancellationToken);
            response.EnsureSuccessStatusCode();
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error setting episode monitored status");
            return false;
        }
    }

    /// <summary>
    /// Get calendar events (upcoming episodes)
    /// </summary>
    public async Task<IList<EpisodeResource>> GetCalendarAsync(
        DateTime? start = null,
        DateTime? end = null,
        bool unmonitored = false,
        bool includeSeries = false,
        bool includeEpisodeFile = false,
        bool includeEpisodeImages = false,
        CancellationToken cancellationToken = default
    )
    {
        var queryBuilder = new QueryBuilder();
        if (start.HasValue)
            queryBuilder.Add("start", start.Value.ToString("O"));
        if (end.HasValue)
            queryBuilder.Add("end", end.Value.ToString("O"));
        if (unmonitored)
            queryBuilder.Add("unmonitored", "true");
        if (includeSeries)
            queryBuilder.Add("includeSeries", "true");
        if (includeEpisodeFile)
            queryBuilder.Add("includeEpisodeFile", "true");
        if (includeEpisodeImages)
            queryBuilder.Add("includeEpisodeImages", "true");

        return await _client.GetFromJsonAsync(
                $"/api/v3/calendar{queryBuilder.ToQueryString()}",
                SonarrClientJsonSerializerContext.Default.IListEpisodeResource,
                cancellationToken
            ) ?? [];
    }

    /// <summary>
    /// Get download queue with pagination
    /// </summary>
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

    /// <summary>
    /// Delete multiple queue items in bulk
    /// </summary>
    public async Task<bool> DeleteQueueItemsBulkAsync(
        IList<int> itemIds,
        bool removeFromClient = true,
        bool blocklist = false,
        CancellationToken cancellationToken = default
    )
    {
        var queryBuilder = new QueryBuilder { { "removeFromClient", removeFromClient.ToString().ToLower() }, { "blocklist", blocklist.ToString().ToLower() } };
        try
        {
            var request = new { ids = itemIds };
            var response = await _client.DeleteAsync($"/api/v3/queue/bulk{queryBuilder.ToQueryString()}", cancellationToken);
            response.EnsureSuccessStatusCode();
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting queue items in bulk");
            return false;
        }
    }

    /// <summary>
    /// Get download history with pagination
    /// </summary>
    public async Task<PagingResource<HistoryResource>> GetHistoryAsync(
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
                $"/api/v3/history{queryBuilder.ToQueryString()}",
                SonarrClientJsonSerializerContext.Default.PagingResourceHistoryResource,
                cancellationToken
            ) ?? new();
    }

    /// <summary>
    /// Get history events since a specific date
    /// </summary>
    public async Task<IList<HistoryResource>> GetHistorySinceAsync(
        DateTime date,
        string? eventType = null,
        bool includeSeries = false,
        bool includeEpisode = false,
        CancellationToken cancellationToken = default
    )
    {
        var queryBuilder = new QueryBuilder { { "date", date.ToString("O") } };
        if (!string.IsNullOrEmpty(eventType))
            queryBuilder.Add("eventType", eventType);
        if (includeSeries)
            queryBuilder.Add("includeSeries", "true");
        if (includeEpisode)
            queryBuilder.Add("includeEpisode", "true");

        return await _client.GetFromJsonAsync(
                $"/api/v3/history/since{queryBuilder.ToQueryString()}",
                SonarrClientJsonSerializerContext.Default.IListHistoryResource,
                cancellationToken
            ) ?? [];
    }

    /// <summary>
    /// Get available releases for an episode
    /// </summary>
    public async Task<IList<ReleaseResource>> GetReleasesAsync(
        int? episodeId = null,
        int? seriesId = null,
        int? seasonNumber = null,
        CancellationToken cancellationToken = default
    )
    {
        var queryBuilder = new QueryBuilder();
        if (episodeId.HasValue)
            queryBuilder.Add("episodeId", episodeId.Value.ToString());
        if (seriesId.HasValue)
            queryBuilder.Add("seriesId", seriesId.Value.ToString());
        if (seasonNumber.HasValue)
            queryBuilder.Add("seasonNumber", seasonNumber.Value.ToString());

        return await _client.GetFromJsonAsync(
                $"/api/v3/release{queryBuilder.ToQueryString()}",
                SonarrClientJsonSerializerContext.Default.IListReleaseResource,
                cancellationToken
            ) ?? [];
    }

    /// <summary>
    /// Grab/download a specific release
    /// </summary>
    public async Task<bool> GrabReleaseAsync(int releaseId, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _client.PostAsJsonAsync($"/api/v3/release/grab/{releaseId}", new { }, cancellationToken);
            response.EnsureSuccessStatusCode();
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error grabbing release {ReleaseId}", releaseId);
            return false;
        }
    }

    /// <summary>
    /// Get all commands/tasks
    /// </summary>
    public async Task<IList<CommandResource>> GetCommandsAsync(CancellationToken cancellationToken = default) =>
        await _client.GetFromJsonAsync("/api/v3/command", SonarrClientJsonSerializerContext.Default.IListCommandResource, cancellationToken) ?? [];

    /// <summary>
    /// Get a specific command by ID
    /// </summary>
    public async Task<CommandResource?> GetCommandAsync(int id, CancellationToken cancellationToken = default) =>
        await _client.GetFromJsonAsync($"/api/v3/command/{id}", SonarrClientJsonSerializerContext.Default.CommandResource, cancellationToken);

    /// <summary>
    /// Execute a command
    /// </summary>
    public async Task<CommandResource?> ExecuteCommandAsync(CommandResource command, CancellationToken cancellationToken = default)
    {
        var response = await _client.PostAsJsonAsync("/api/v3/command", command, SonarrClientJsonSerializerContext.Default.CommandResource, cancellationToken);
        return await response.Content.ReadFromJsonAsync(SonarrClientJsonSerializerContext.Default.CommandResource, cancellationToken);
    }

    /// <summary>
    /// Search for a series by series ID
    /// </summary>
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

    /// <summary>
    /// Search for a specific season in a series
    /// </summary>
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

    /// <summary>
    /// Search for specific episodes
    /// </summary>
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

    // implement command with these payload

    // payload = {"name": "SeriesSearch", "seriesId": series_id}

    // payload = {
    //         "name": "SeasonSearch",
    //         "seriesId": series_id,
    //         "seasonNumber": season_number
    //     }

    // payload = {
    //     "name": "EpisodeSearch",
    //     "episodeIds": episode_ids
    // }
}

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, UseStringEnumConverter = true)]
[JsonSerializable(typeof(IList<QualityProfileResource>))]
[JsonSerializable(typeof(SystemResource))]
[JsonSerializable(typeof(PagingResource<EpisodeResource>))]
[JsonSerializable(typeof(IList<SeriesResource>))]
[JsonSerializable(typeof(SeriesResource))]
[JsonSerializable(typeof(PagingResource<SonarrQueueResource>))]
[JsonSerializable(typeof(IList<HealthResource>))]
[JsonSerializable(typeof(IList<EpisodeResource>))]
[JsonSerializable(typeof(EpisodeResource))]
[JsonSerializable(typeof(EpisodeFileResource))]
[JsonSerializable(typeof(PagingResource<HistoryResource>))]
[JsonSerializable(typeof(IList<HistoryResource>))]
[JsonSerializable(typeof(IList<ReleaseResource>))]
[JsonSerializable(typeof(IList<CommandResource>))]
[JsonSerializable(typeof(CommandResource))]
[JsonSerializable(typeof(SeriesSearchCommand))]
[JsonSerializable(typeof(SeasonSearchCommand))]
[JsonSerializable(typeof(EpisodeSearchCommand))]
internal partial class SonarrClientJsonSerializerContext : JsonSerializerContext;
