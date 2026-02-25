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
}

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, UseStringEnumConverter = true)]
[JsonSerializable(typeof(IList<MovieResource>))]
[JsonSerializable(typeof(MovieResource))]
[JsonSerializable(typeof(PagingResource<RadarrQueueResource>))]
[JsonSerializable(typeof(CommandResource))]
[JsonSerializable(typeof(MoviesSearchCommand))]
internal partial class RadarrClientJsonSerializerContext : JsonSerializerContext;
