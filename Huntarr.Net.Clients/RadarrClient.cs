using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Huntarr.Net.Clients.Models;
using Huntarr.Net.Clients.Options;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Huntarr.Net.Clients;

public class RadarrClient
{
    private readonly HttpClient _client;
    private readonly ILogger<RadarrClient> _logger;

    public RadarrClient(HttpClient client, IOptionsSnapshot<RadarrOptions> options, ILogger<RadarrClient> logger)
    {
        _client = client;
        _client.BaseAddress = new Uri(options.Value.BaseUrl);
        if (!string.IsNullOrEmpty(options.Value.ApiKey))
        {
            _client.DefaultRequestHeaders.Add("X-Api-Key", options.Value.ApiKey);
        }
        _logger = logger;
    }

    /// <summary>
    /// Get system status information
    /// </summary>
    public async Task<SystemResource?> GetSystemInfoAsync(CancellationToken cancellationToken = default) =>
        await _client.GetFromJsonAsync("/api/v3/system/status", RadarrClientJsonSerializerContext.Default.SystemResource, cancellationToken);

    /// <summary>
    /// Get all quality profiles
    /// </summary>
    public async Task<IList<QualityProfileResource>> GetQualityProfilesAsync(CancellationToken cancellationToken = default) =>
        await _client.GetFromJsonAsync("/api/v3/qualityprofile", RadarrClientJsonSerializerContext.Default.IListQualityProfileResource, cancellationToken)
        ?? [];

    /// <summary>
    /// Get all movies
    /// </summary>
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

    /// <summary>
    /// Get a specific movie by ID
    /// </summary>
    public async Task<MovieResource?> GetMovieByIdAsync(int id, CancellationToken cancellationToken = default) =>
        await _client.GetFromJsonAsync($"/api/v3/movie/{id}", RadarrClientJsonSerializerContext.Default.MovieResource, cancellationToken);

    /// <summary>
    /// Add a new movie
    /// </summary>
    public async Task<MovieResource?> AddMovieAsync(MovieResource movie, CancellationToken cancellationToken = default)
    {
        var response = await _client.PostAsJsonAsync("/api/v3/movie", movie, RadarrClientJsonSerializerContext.Default.MovieResource, cancellationToken);
        return await response.Content.ReadFromJsonAsync(RadarrClientJsonSerializerContext.Default.MovieResource, cancellationToken);
    }

    /// <summary>
    /// Update an existing movie
    /// </summary>
    public async Task<MovieResource?> UpdateMovieAsync(MovieResource movie, bool moveFiles = false, CancellationToken cancellationToken = default)
    {
        var queryBuilder = new QueryBuilder { { "moveFiles", moveFiles.ToString().ToLower() } };
        var response = await _client.PutAsJsonAsync(
            $"/api/v3/movie/{movie.Id}{queryBuilder.ToQueryString()}",
            movie,
            RadarrClientJsonSerializerContext.Default.MovieResource,
            cancellationToken
        );
        return await response.Content.ReadFromJsonAsync(RadarrClientJsonSerializerContext.Default.MovieResource, cancellationToken);
    }

    /// <summary>
    /// Delete a movie
    /// </summary>
    public async Task<bool> DeleteMovieAsync(int id, bool deleteFiles = false, bool addImportExclusion = false, CancellationToken cancellationToken = default)
    {
        var queryBuilder = new QueryBuilder
        {
            { "deleteFiles", deleteFiles.ToString().ToLower() },
            { "addImportExclusion", addImportExclusion.ToString().ToLower() },
        };
        try
        {
            var response = await _client.DeleteAsync($"/api/v3/movie/{id}{queryBuilder.ToQueryString()}", cancellationToken);
            response.EnsureSuccessStatusCode();
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting movie {MovieId}", id);
            return false;
        }
    }

    /// <summary>
    /// Search for movies by term
    /// </summary>
    public async Task<IList<MovieResource>> SearchMoviesAsync(string term, CancellationToken cancellationToken = default)
    {
        var queryBuilder = new QueryBuilder { { "term", term } };
        return await _client.GetFromJsonAsync(
                $"/api/v3/movie/lookup{queryBuilder.ToQueryString()}",
                RadarrClientJsonSerializerContext.Default.IListMovieResource,
                cancellationToken
            ) ?? [];
    }

    /// <summary>
    /// Search for movies by TMDB ID
    /// </summary>
    public async Task<MovieResource?> SearchMovieByTmdbAsync(int tmdbId, CancellationToken cancellationToken = default)
    {
        var queryBuilder = new QueryBuilder { { "tmdbId", tmdbId.ToString() } };
        return await _client.GetFromJsonAsync(
            $"/api/v3/movie/lookup/tmdb{queryBuilder.ToQueryString()}",
            RadarrClientJsonSerializerContext.Default.MovieResource,
            cancellationToken
        );
    }

    /// <summary>
    /// Search for movies by IMDb ID
    /// </summary>
    public async Task<MovieResource?> SearchMovieByImdbAsync(string imdbId, CancellationToken cancellationToken = default)
    {
        var queryBuilder = new QueryBuilder { { "imdbId", imdbId } };
        return await _client.GetFromJsonAsync(
            $"/api/v3/movie/lookup/imdb{queryBuilder.ToQueryString()}",
            RadarrClientJsonSerializerContext.Default.MovieResource,
            cancellationToken
        );
    }

    /// <summary>
    /// Get calendar events (upcoming releases)
    /// </summary>
    public async Task<IList<MovieResource>> GetCalendarAsync(
        DateTime? start = null,
        DateTime? end = null,
        bool unmonitored = false,
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

        return await _client.GetFromJsonAsync(
                $"/api/v3/calendar{queryBuilder.ToQueryString()}",
                RadarrClientJsonSerializerContext.Default.IListMovieResource,
                cancellationToken
            ) ?? [];
    }

    /// <summary>
    /// Get missing movies
    /// </summary>
    public async Task<PagingResource<MovieResource>> GetMissingMoviesAsync(
        int page = 1,
        int pageSize = 10,
        string? sortKey = null,
        string? sortDirection = null,
        bool monitored = true,
        CancellationToken cancellationToken = default
    )
    {
        var queryBuilder = new QueryBuilder { { "page", page.ToString() }, { "pageSize", pageSize.ToString() } };
        if (!string.IsNullOrEmpty(sortKey))
            queryBuilder.Add("sortKey", sortKey);
        if (!string.IsNullOrEmpty(sortDirection))
            queryBuilder.Add("sortDirection", sortDirection);
        if (!monitored)
            queryBuilder.Add("monitored", "false");

        return await _client.GetFromJsonAsync(
                $"/api/v3/wanted/missing{queryBuilder.ToQueryString()}",
                RadarrClientJsonSerializerContext.Default.PagingResourceMovieResource,
                cancellationToken
            ) ?? new();
    }

    /// <summary>
    /// Get download queue with pagination
    /// </summary>
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

    /// <summary>
    /// Delete a queue item
    /// </summary>
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

    /// <summary>
    /// Get available releases for a movie
    /// </summary>
    public async Task<IList<ReleaseResource>> GetReleasesAsync(int movieId, CancellationToken cancellationToken = default)
    {
        var queryBuilder = new QueryBuilder { { "movieId", movieId.ToString() } };
        return await _client.GetFromJsonAsync(
                $"/api/v3/release{queryBuilder.ToQueryString()}",
                RadarrClientJsonSerializerContext.Default.IListReleaseResource,
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
    /// Get download history with pagination
    /// </summary>
    public async Task<PagingResource<RadarrHistoryResource>> GetHistoryAsync(
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
                $"/api/v3/history{queryBuilder.ToQueryString()}",
                RadarrClientJsonSerializerContext.Default.PagingResourceRadarrHistoryResource,
                cancellationToken
            ) ?? new();
    }

    /// <summary>
    /// Get history events since a specific date
    /// </summary>
    public async Task<IList<RadarrHistoryResource>> GetHistorySinceAsync(
        DateTime date,
        string? eventType = null,
        bool includeMovie = false,
        CancellationToken cancellationToken = default
    )
    {
        var queryBuilder = new QueryBuilder { { "date", date.ToString("O") } };
        if (!string.IsNullOrEmpty(eventType))
            queryBuilder.Add("eventType", eventType);
        if (includeMovie)
            queryBuilder.Add("includeMovie", "true");

        return await _client.GetFromJsonAsync(
                $"/api/v3/history/since{queryBuilder.ToQueryString()}",
                RadarrClientJsonSerializerContext.Default.IListRadarrHistoryResource,
                cancellationToken
            ) ?? [];
    }

    /// <summary>
    /// Get all commands/tasks
    /// </summary>
    public async Task<IList<CommandResource>> GetCommandsAsync(CancellationToken cancellationToken = default) =>
        await _client.GetFromJsonAsync("/api/v3/command", RadarrClientJsonSerializerContext.Default.IListCommandResource, cancellationToken) ?? [];

    /// <summary>
    /// Get a specific command by ID
    /// </summary>
    public async Task<CommandResource?> GetCommandAsync(int id, CancellationToken cancellationToken = default) =>
        await _client.GetFromJsonAsync($"/api/v3/command/{id}", RadarrClientJsonSerializerContext.Default.CommandResource, cancellationToken);

    /// <summary>
    /// Get all health checks
    /// </summary>
    public async Task<IList<HealthResource>> GetHealthAsync(CancellationToken cancellationToken = default) =>
        await _client.GetFromJsonAsync("/api/v3/health", RadarrClientJsonSerializerContext.Default.IListHealthResource, cancellationToken) ?? [];

    /// <summary>
    /// Search for specific movies
    /// </summary>
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
}

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, UseStringEnumConverter = true)]
[JsonSerializable(typeof(SystemResource))]
[JsonSerializable(typeof(IList<QualityProfileResource>))]
[JsonSerializable(typeof(IList<MovieResource>))]
[JsonSerializable(typeof(MovieResource))]
[JsonSerializable(typeof(PagingResource<MovieResource>))]
[JsonSerializable(typeof(PagingResource<RadarrQueueResource>))]
[JsonSerializable(typeof(IList<ReleaseResource>))]
[JsonSerializable(typeof(PagingResource<RadarrHistoryResource>))]
[JsonSerializable(typeof(IList<RadarrHistoryResource>))]
[JsonSerializable(typeof(IList<CommandResource>))]
[JsonSerializable(typeof(CommandResource))]
[JsonSerializable(typeof(IList<HealthResource>))]
[JsonSerializable(typeof(MoviesSearchCommand))]
internal partial class RadarrClientJsonSerializerContext : JsonSerializerContext;
