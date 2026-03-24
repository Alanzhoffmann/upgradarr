using System.Text.Json.Serialization;
using Upgradarr.Apps.Enums;
using Upgradarr.Apps.Models;
using Upgradarr.Domain.Enums;
using Upgradarr.Domain.Interfaces;
using Upgradarr.Domain.ValueObjects;

namespace Upgradarr.Apps.Sonarr.Models;

public record SonarrQueueResource : IQueueResource
{
    public DateTimeOffset? Added { get; init; }

    // public IEnumerable<CustomFormatResource>? CustomFormats { get; init; }
    public int CustomFormatScore { get; init; }
    public string? DownloadClient { get; init; }
    public bool DownloadClientHasPostImportCategory { get; init; }
    public string? DownloadId { get; init; }
    public EpisodeResource? Episode { get; init; }
    public bool EpisodeHasFile { get; init; }
    public int? EpisodeId { get; init; }
    public string? ErrorMessage { get; init; }
    public DateTimeOffset? EstimatedCompletionTime { get; init; }
    public int Id { get; init; }
    public string? Indexer { get; init; }
    public IEnumerable<Language>? Languages { get; init; }
    public string? OutputPath { get; init; }
    public DownloadProtocol Protocol { get; init; }

    public QualityModel? Quality { get; init; }
    public int? SeasonNumber { get; init; }
    public SeriesResource? Series { get; init; }
    public int? SeriesId { get; init; }
    public double? Size { get; init; }
    public QueueStatus Status { get; init; }
    public IEnumerable<TrackedDownloadStatusMessage>? StatusMessages { get; init; }
    public string? Title { get; init; }
    public TrackedDownloadState TrackedDownloadState { get; init; }
    public TrackedDownloadStatus TrackedDownloadStatus { get; init; }

    [JsonIgnore]
    public RecordSource Source => RecordSource.Sonarr;
}
