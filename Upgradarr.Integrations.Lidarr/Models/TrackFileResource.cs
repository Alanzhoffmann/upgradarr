using Upgradarr.Integrations.Models;

namespace Upgradarr.Integrations.Lidarr.Models;

public record TrackFileResource
{
    public int Id { get; init; }
    public int ArtistId { get; init; }
    public int AlbumId { get; init; }
    public string? Path { get; init; }
    public long Size { get; init; }
    public DateTimeOffset DateAdded { get; init; }
    public string? SceneName { get; init; }
    public string? ReleaseGroup { get; init; }
    public QualityModel? Quality { get; init; }
    public int CustomFormatScore { get; init; }
    public bool QualityCutoffNotMet { get; init; }
}
