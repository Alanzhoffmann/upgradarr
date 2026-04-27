using System.Text.Json.Serialization;
using Upgradarr.Domain.Enums;
using Upgradarr.Integrations.Enums;
using Upgradarr.Integrations.Interfaces;
using Upgradarr.Integrations.Models;

namespace Upgradarr.Integrations.Lidarr.Models;

public record LidarrQueueResource : IQueueResource
{
    public DateTimeOffset? Added { get; init; }

    // public IEnumerable<CustomFormatResource>? CustomFormats { get; init; }
    public int CustomFormatScore { get; init; }
    public string? DownloadClient { get; init; }
    public bool DownloadClientHasPostImportCategory { get; init; }
    public string? DownloadId { get; init; }
    public string? ErrorMessage { get; init; }
    public DateTimeOffset? EstimatedCompletionTime { get; init; }
    public int Id { get; init; }
    public string? Indexer { get; init; }
    public string? OutputPath { get; init; }
    public DownloadProtocol Protocol { get; init; }
    public int? ArtistId { get; init; }
    public int? AlbumId { get; init; }
    public ArtistResource? Artist { get; init; }
    public AlbumResource? Album { get; init; }
    public double? Size { get; init; }
    public QueueStatus Status { get; init; }
    public QualityModel? Quality { get; init; }
    public IEnumerable<TrackedDownloadStatusMessage>? StatusMessages { get; init; }
    public string? Title { get; init; }
    public TrackedDownloadState TrackedDownloadState { get; init; }
    public TrackedDownloadStatus TrackedDownloadStatus { get; init; }

    [JsonIgnore]
    public RecordSource Source => RecordSource.Lidarr;
}
