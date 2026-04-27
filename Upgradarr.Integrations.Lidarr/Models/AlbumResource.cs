using Upgradarr.Integrations.Models;

namespace Upgradarr.Integrations.Lidarr.Models;

public record AlbumResource
{
    public int Id { get; init; }
    public string? Title { get; init; }
    public string? Disambiguation { get; init; }
    public string? Overview { get; init; }
    public int ArtistId { get; init; }
    public ArtistResource? Artist { get; init; }
    public string? ForeignAlbumId { get; init; }
    public bool Monitored { get; init; }
    public bool AnyReleaseOk { get; init; }
    public int ProfileId { get; init; }
    public int Duration { get; init; }
    public string? AlbumType { get; init; }
    public IEnumerable<string>? SecondaryTypes { get; init; }
    public Ratings? Ratings { get; init; }
    public DateTimeOffset? ReleaseDate { get; init; }
    public IEnumerable<string>? Genres { get; init; }
    public IEnumerable<MediaCover>? Images { get; init; }
    public string? RemoteCover { get; init; }
    public AlbumStatisticsResource? Statistics { get; init; }
}
