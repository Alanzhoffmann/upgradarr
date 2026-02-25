namespace Huntarr.Net.Clients.Models;

public class EpisodeFileResource
{
    public int Id { get; set; }
    public int SeriesId { get; set; }
    public int SeasonNumber { get; set; }
    public string? RelativePath { get; set; }
    public string? Path { get; set; }
    public long Size { get; set; }
    public DateTime DateAdded { get; set; }
    public string? SceneName { get; set; }
    public string? ReleaseGroup { get; set; }
    public IList<Language>? Languages { get; set; }
    public QualityModel? Quality { get; set; }
    public int CustomFormatScore { get; set; }
    public int? IndexerFlags { get; set; }
    public string? ReleaseType { get; set; }
    public MediaInfoResource? MediaInfo { get; set; }
    public bool QualityCutoffNotMet { get; set; }
}

public class MediaInfoResource
{
    public int Id { get; set; }
    public long AudioBitrate { get; set; }
    public double AudioChannels { get; set; }
    public string? AudioCodec { get; set; }
    public string? AudioLanguages { get; set; }
    public int AudioStreamCount { get; set; }
    public int VideoBitDepth { get; set; }
    public long VideoBitrate { get; set; }
    public string? VideoCodec { get; set; }
    public double VideoFps { get; set; }
    public string? VideoDynamicRange { get; set; }
    public string? VideoDynamicRangeType { get; set; }
    public string? Resolution { get; set; }
    public string? RunTime { get; set; }
    public string? ScanType { get; set; }
    public string? Subtitles { get; set; }
}
