namespace Upgradarr.Integrations.Lidarr.Options;

public record LidarrOptions
{
    public const string SectionName = "Lidarr";

    public required string BaseUrl { get; init; } = "http://lidarr:8686";
    public string? ApiKey { get; init; }
}
