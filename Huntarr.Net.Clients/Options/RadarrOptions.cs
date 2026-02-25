namespace Huntarr.Net.Clients.Options;

public record RadarrOptions
{
    public const string SectionName = "Radarr";

    public required string BaseUrl { get; init; } = "http://radarr:7878";
    public string? ApiKey { get; init; }
}
