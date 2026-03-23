using System.Text.Json.Serialization;
using Huntarr.Net.Clients.Models;
using Upgradarr.Apps.Enums;
using Upgradarr.Apps.Interfaces;
using Upgradarr.Apps.Models;

namespace Upgradarr.Apps.Radarr.Models;

public record RadarrQueueResource : IQueueResource
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
    public IEnumerable<Language>? Languages { get; init; }
    public string? OutputPath { get; init; }
    public DownloadProtocol Protocol { get; init; }
    public int? MovieId { get; init; }
    public double? Size { get; init; }
    public QueueStatus Status { get; init; }
    public QualityModel? Quality { get; init; }
    public IEnumerable<TrackedDownloadStatusMessage>? StatusMessages { get; init; }
    public string? Title { get; init; }
    public TrackedDownloadState TrackedDownloadState { get; init; }
    public TrackedDownloadStatus TrackedDownloadStatus { get; init; }

    [JsonIgnore]
    public RecordSource Source => RecordSource.Radarr;
}
