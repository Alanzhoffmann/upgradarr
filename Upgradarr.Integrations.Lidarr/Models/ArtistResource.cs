using Upgradarr.Integrations.Models;

namespace Upgradarr.Integrations.Lidarr.Models;

public record ArtistResource
{
    public int Id { get; init; }
    public string? ArtistName { get; init; }
    public string? SortName { get; init; }
    public string? Overview { get; init; }
    public string? ArtistType { get; init; }
    public string? Disambiguation { get; init; }
    public string? ForeignArtistId { get; init; }
    public bool Monitored { get; init; }
    public string? Path { get; init; }
    public string? RootFolderPath { get; init; }
    public string? Folder { get; init; }
    public int QualityProfileId { get; init; }
    public int MetadataProfileId { get; init; }
    public IEnumerable<string>? Genres { get; init; }
    public IEnumerable<MediaCover>? Images { get; init; }
    public string? RemotePoster { get; init; }
    public IEnumerable<int>? Tags { get; init; }
    public DateTimeOffset Added { get; init; }
    public Ratings? Ratings { get; init; }
    public ArtistStatisticsResource? Statistics { get; init; }
}
