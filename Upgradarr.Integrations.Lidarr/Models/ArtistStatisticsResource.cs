namespace Upgradarr.Integrations.Lidarr.Models;

public record ArtistStatisticsResource
{
    public int AlbumCount { get; init; }
    public int TrackFileCount { get; init; }
    public int TrackCount { get; init; }
    public int TotalTrackCount { get; init; }
    public long SizeOnDisk { get; init; }
    public double PercentOfTracks { get; init; }
}
