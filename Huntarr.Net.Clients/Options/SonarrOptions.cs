namespace Huntarr.Net.Clients.Options;

public record SonarrOptions
{
    public const string SectionName = "Sonarr";

    public required string BaseUrl { get; init; } = "http://sonarr:8989";
    public string? ApiKey { get; init; }
    public int RefreshTimeoutMinutes { get; init; } = 5;
}
