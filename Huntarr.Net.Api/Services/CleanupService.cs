using System.Runtime.CompilerServices;
using Huntarr.Net.Api.Models;
using Huntarr.Net.Api.Options;
using Huntarr.Net.Clients;
using Huntarr.Net.Clients.Enums;
using Huntarr.Net.Clients.Interfaces;
using Huntarr.Net.Clients.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Huntarr.Net.Api.Services;

public class CleanupService
{
    private readonly SonarrClient _sonarrClient;
    private readonly RadarrClient _radarrClient;
    private readonly CleanupOptions _options;
    private readonly ILogger<CleanupService> _logger;
    private readonly AppDbContext _dbContext;
    private readonly TimeProvider _timeProvider;

    private static readonly string[] _stalledErrorMessages = ["The download is stalled with no connections", "metadata"];

    private static readonly string[] _immediateRemovalErrorMessages =
    [
        "Invalid video file",
        "Unable to determine if file is a sample",
        "Not a Custom Format upgrade",
        "Not an upgrade",
        "Caution: Found executable file with extension",
        "Unable to parse file",
    ];

    private static readonly string[] _weirdExtensions =
    [
        ".lnk",
        ".exe",
        ".bat",
        ".cmd",
        ".scr",
        ".pif",
        ".com",
        ".zipx",
        ".jar",
        ".vbs",
        ".js",
        ".jse",
        ".wsf",
        ".wsh",
    ];

    public CleanupService(
        SonarrClient sonarrClient,
        RadarrClient radarrClient,
        IOptionsSnapshot<CleanupOptions> options,
        ILogger<CleanupService> logger,
        AppDbContext dbContext,
        TimeProvider timeProvider
    )
    {
        _sonarrClient = sonarrClient;
        _radarrClient = radarrClient;
        _options = options.Value;
        _logger = logger;
        _dbContext = dbContext;
        _timeProvider = timeProvider;
    }

    public async Task PerformCleanupAsync(CancellationToken cancellationToken = default)
    {
        var queueItems = GetAllQueueItems(cancellationToken);
        var trackedDownloads = await _dbContext.TrackedDownloads.ToDictionaryAsync(r => r.DownloadId, cancellationToken);
        var currentQueueDownloadIds = new HashSet<string>();

        await foreach (var item in queueItems)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                break;
            }

            if (item.DownloadId is null)
            {
                continue;
            }

            currentQueueDownloadIds.Add(item.DownloadId);

            var queueRecord = trackedDownloads.GetValueOrDefault(item.DownloadId);
            if (queueRecord is null)
            {
                queueRecord = new QueueRecord
                {
                    DownloadId = item.DownloadId,
                    Title = item.Title,
                    Added = item.Added ?? DateTimeOffset.UtcNow,
                    Source = item.Source,
                    ItemScores = [new QueueItemScore { ItemId = item.Id, CustomFormatScore = item.CustomFormatScore }],
                };
                _dbContext.TrackedDownloads.Add(queueRecord);
                trackedDownloads[item.DownloadId] = queueRecord;
            }

            await ProcessQueueItemAsync(item, queueRecord, cancellationToken);
            _logger.LogRecord(item);
        }

        // Remove tracked downloads for items no longer in queue
        var removedItems = trackedDownloads.Values.Where(r => !currentQueueDownloadIds.Contains(r.DownloadId)).ToList();
        foreach (var removedItem in removedItems)
        {
            _logger.LogQueueItemRemoved(removedItem.Title, removedItem.DownloadId);
            _dbContext.TrackedDownloads.Remove(removedItem);
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task ProcessQueueItemAsync(IQueueResource item, QueueRecord queueRecord, CancellationToken cancellationToken)
    {
        // Update or add the custom format score for this item
        var existingScore = queueRecord.ItemScores.FirstOrDefault(s => s.ItemId == item.Id);
        if (existingScore is not null)
        {
            existingScore.CustomFormatScore = item.CustomFormatScore;
        }
        else
        {
            queueRecord.ItemScores.Add(new QueueItemScore { ItemId = item.Id, CustomFormatScore = item.CustomFormatScore });
        }

        // Check for immediate removal conditions
        if (await ShouldRemoveImmediatelyAsync(item, cancellationToken))
        {
            _logger.LogCleaningUpQueueItem(item.Title);
            queueRecord.MarkForRemoval(_timeProvider.GetUtcNow());
            return;
        }

        // Skip cleanup for items added less than an hour ago
        TimeSpan durationSinceAdded = _timeProvider.GetUtcNow() - (item.Added ?? DateTimeOffset.MinValue);
        if (durationSinceAdded.TotalHours < 1)
        {
            _logger.LogSkippingRecentQueueItem(item.Title);
            queueRecord.ClearRemoval();
            return;
        }

        // Check if item should be marked as failed and schedule removal
        if (ShouldMarkAsFailedForItem(item))
        {
            if (!queueRecord.RemoveAt.HasValue)
            {
                // First time marking as failed - schedule removal for future
                var futureRemovalTime = _timeProvider.GetUtcNow().AddHours(_options.FailedDownloadCleanupHours);
                queueRecord.MarkForRemoval(futureRemovalTime);
                _logger.LogScheduledForRemoval(item.Title, futureRemovalTime);
            }
            // If already scheduled, keep existing removal time
        }
        else if (queueRecord.RemoveAt.HasValue)
        {
            // Item is no longer failing, clear scheduled removal
            _logger.LogFailureStateReset(item.Title);
            queueRecord.ClearRemoval();
        }
    }

    private bool ShouldMarkAsFailedForItem(IQueueResource item)
    {
        if (!item.EstimatedCompletionTime.HasValue)
        {
            return true;
        }

        TimeSpan durationSinceEstimatedCompletion = _timeProvider.GetUtcNow() - item.EstimatedCompletionTime.Value;
        if (item.EstimatedCompletionTime.HasValue && durationSinceEstimatedCompletion.TotalHours > _options.MaxDownloadTimeHours)
        {
            return true;
        }

        if (_stalledErrorMessages.Any(msg => item.HasErrorMessage(msg)))
        {
            return true;
        }

        if (item.Status is QueueStatus.Failed || item.Status is QueueStatus.Queued)
        {
            return true;
        }

        return false;
    }

    private async Task<bool> ShouldRemoveImmediatelyAsync(IQueueResource item, CancellationToken cancellationToken)
    {
        // Check max age - remove if too old
        if (item.Added.HasValue)
        {
            TimeSpan durationSinceAdded = _timeProvider.GetUtcNow() - item.Added.Value;
            if (durationSinceAdded.TotalDays > _options.MaxItemAgeDays)
            {
                return true;
            }
        }

        if (_immediateRemovalErrorMessages.Any(msg => item.HasErrorMessage(msg)))
        {
            return true;
        }

        if (_weirdExtensions.Any(e => Path.GetExtension(item.OutputPath) == e))
        {
            return true;
        }

        // Check custom format score against downloaded episodes/movies
        if (item is SonarrQueueResource sonarrItem && sonarrItem.EpisodeId.HasValue)
        {
            var episodes = await _sonarrClient.GetEpisodesAsync(
                episodeIds: [sonarrItem.EpisodeId.Value],
                includeEpisodeFile: true,
                cancellationToken: cancellationToken
            );

            var episode = episodes.FirstOrDefault();
            if (episode?.EpisodeFile is not null)
            {
                // Episode has a downloaded file, compare scores
                var downloadedScore = episode.EpisodeFile.CustomFormatScore;
                if (
                    item.CustomFormatScore <= downloadedScore
                    && (
                        sonarrItem.Quality?.Quality is null
                        || episode.EpisodeFile.Quality?.Quality is null
                        || sonarrItem.Quality.Quality.Resolution <= episode.EpisodeFile.Quality.Quality.Resolution
                    )
                )
                {
                    _logger.LogLowerCustomFormatScore(item.Title, item.CustomFormatScore, downloadedScore);
                    return true;
                }
            }
        }
        else if (item is RadarrQueueResource radarrItem && radarrItem.MovieId.HasValue)
        {
            var movie = await _radarrClient.GetMovieByIdAsync(radarrItem.MovieId.Value, cancellationToken);

            if (movie?.MovieFile is not null)
            {
                // Movie has a downloaded file, compare scores
                var downloadedScore = movie.MovieFile.CustomFormatScore ?? 0;
                if (
                    item.CustomFormatScore <= downloadedScore
                    && (
                        radarrItem.Quality?.Quality is null
                        || movie.MovieFile.Quality?.Quality is null
                        || radarrItem.Quality.Quality.Resolution <= movie.MovieFile.Quality.Quality.Resolution
                    )
                )
                {
                    _logger.LogLowerCustomFormatScore(item.Title, item.CustomFormatScore, downloadedScore);
                    return true;
                }
            }
        }

        return false;
    }

    private async IAsyncEnumerable<IQueueResource> GetAllQueueItems([EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var sonarrItems = await _sonarrClient.GetQueueAsync(cancellationToken);
        foreach (var sonarrItem in sonarrItems.Records)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                yield break;
            }

            if (sonarrItem.DownloadId is null)
            {
                // Skip items with null DownloadId, as these cannot be tracked for cleanup
                continue;
            }

            if (sonarrItem.DownloadId.Equals(sonarrItem.Title, StringComparison.OrdinalIgnoreCase) && sonarrItem.EpisodeId.HasValue)
            {
                var episode = await _sonarrClient.GetEpisodeByIdAsync(sonarrItem.EpisodeId.Value, cancellationToken);
                if (episode is not null)
                {
                    yield return sonarrItem with
                    {
                        Title = $"{episode.Series?.Title} {episode.SeasonNumber}x{episode.EpisodeNumber:00} - {episode.Title}",
                    };
                    continue;
                }
            }

            yield return sonarrItem;
        }

        var radarrItems = await _radarrClient.GetQueueAsync(cancellationToken: cancellationToken);
        foreach (var radarrItem in radarrItems.Records)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                yield break;
            }

            if (radarrItem.DownloadId is null)
            {
                // Skip items with null DownloadId, as these cannot be tracked for cleanup
                continue;
            }

            if (radarrItem.DownloadId.Equals(radarrItem.Title, StringComparison.OrdinalIgnoreCase) && radarrItem.MovieId.HasValue)
            {
                var movie = await _radarrClient.GetMovieByIdAsync(radarrItem.MovieId.Value, cancellationToken);
                if (movie is not null)
                {
                    yield return radarrItem with
                    {
                        Title = movie.Title,
                    };
                    continue;
                }
            }

            yield return radarrItem;
        }
    }
}

internal static partial class CleanupLoggerExtensions
{
    [LoggerMessage(EventId = 0, Level = LogLevel.Information, Message = "Skipping cleanup for queue item with Title: {Title} added less than an hour ago.")]
    public static partial void LogSkippingRecentQueueItem(this ILogger logger, string? title);

    [LoggerMessage(EventId = 1, Level = LogLevel.Information, Message = "Cleaning up queue item with Title: {Title}.")]
    public static partial void LogCleaningUpQueueItem(this ILogger logger, string? title);

    [LoggerMessage(EventId = 2, Level = LogLevel.Debug, Message = "Record: {Record}")]
    public static partial void LogRecord(this ILogger logger, IQueueResource record);

    [LoggerMessage(EventId = 3, Level = LogLevel.Information, Message = "Download with Title: {Title} is progressing normally. Failure state reset.")]
    public static partial void LogFailureStateReset(this ILogger logger, string? title);

    [LoggerMessage(
        EventId = 4,
        Level = LogLevel.Information,
        Message = "Queue item with Title: {Title} has lower custom format score ({CurrentScore}) than previous ({PreviousScore}). Removing."
    )]
    public static partial void LogLowerCustomFormatScore(this ILogger logger, string? title, int currentScore, int previousScore);

    [LoggerMessage(
        EventId = 5,
        Level = LogLevel.Information,
        Message = "Queue item with Title: {Title} (DownloadId: {DownloadId}) no longer in queue. Removing tracked record."
    )]
    public static partial void LogQueueItemRemoved(this ILogger logger, string? title, string? downloadId);

    [LoggerMessage(EventId = 6, Level = LogLevel.Information, Message = "Queue item with Title: {Title} scheduled for removal at {RemoveAt}.")]
    public static partial void LogScheduledForRemoval(this ILogger logger, string? title, DateTimeOffset removeAt);
}
