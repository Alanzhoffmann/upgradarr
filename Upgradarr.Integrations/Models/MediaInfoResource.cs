namespace Upgradarr.Integrations.Models;

public record MediaInfoResource
{
    public int Id { get; init; }
    public long AudioBitrate { get; init; }
    public double AudioChannels { get; init; }
    public string? AudioCodec { get; init; }
    public string? AudioLanguages { get; init; }
    public int AudioStreamCount { get; init; }
    public int VideoBitDepth { get; init; }
    public long VideoBitrate { get; init; }
    public string? VideoCodec { get; init; }
    public double VideoFps { get; init; }
    public string? VideoDynamicRange { get; init; }
    public string? VideoDynamicRangeType { get; init; }
    public string? Resolution { get; init; }
    public string? RunTime { get; init; }
    public string? ScanType { get; init; }
    public string? Subtitles { get; init; }
}
