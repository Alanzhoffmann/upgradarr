using System.Runtime.CompilerServices;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Upgradarr.Application.Options;
using Upgradarr.Data;
using Upgradarr.Data.Interfaces;
using Upgradarr.Domain.Entities;
using Upgradarr.Domain.Enums;
using Upgradarr.Domain.Interfaces;
using Upgradarr.Domain.ValueObjects;
using Upgradarr.Integrations.Enums;
using Upgradarr.Integrations.Interfaces;

namespace Upgradarr.Application.Services;

public partial class CleanupService(
    IOptionsSnapshot<CleanupOptions> options,
    ILogger<CleanupService> logger,
    AppDbContext dbContext,
    TimeProvider timeProvider,
    IEnumerable<IQueueManager> queueManagers,
    IMigrationState migrationState
    ) : ICleanupService
{
    private readonly CleanupOptions _options = options.Value;
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

    public async Task PerformCleanupAsync(CancellationToken cancellationToken = default)
    {
        if (!migrationState.IsDone)
            return;

        var queueItems = GetAllQueueItems(cancellationToken);
        var trackedDownloads = await dbContext.TrackedDownloads.ToDictionaryAsync(r => r.DownloadId, cancellationToken);
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
                dbContext.TrackedDownloads.Add(queueRecord);
                trackedDownloads[item.DownloadId] = queueRecord;
            }

            await ProcessQueueItemAsync(item, queueRecord, cancellationToken);
            LogRecord(item);
        }

        // Remove tracked downloads for items no longer in queue
        var removedItems = trackedDownloads.Values.Where(r => !currentQueueDownloadIds.Contains(r.DownloadId)).ToList();
        foreach (var removedItem in removedItems)
        {
            LogQueueItemRemoved(removedItem.Title, removedItem.DownloadId);
            dbContext.TrackedDownloads.Remove(removedItem);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
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
            LogCleaningUpQueueItem(item.Title);
            queueRecord.MarkForRemoval(timeProvider.GetUtcNow());
            return;
        }

        // Skip cleanup for items added less than an hour ago
        TimeSpan durationSinceAdded = timeProvider.GetUtcNow() - (item.Added ?? DateTimeOffset.MinValue);
        if (durationSinceAdded.TotalHours < 1)
        {
            LogSkippingRecentQueueItem(item.Title);
            queueRecord.ClearRemoval();
            return;
        }

        // Check if item should be marked as failed and schedule removal
        if (ShouldMarkAsFailedForItem(item))
        {
            if (!queueRecord.RemoveAt.HasValue)
            {
                // First time marking as failed - schedule removal for future
                var futureRemovalTime = timeProvider.GetUtcNow().AddHours(_options.FailedDownloadCleanupHours);
                queueRecord.MarkForRemoval(futureRemovalTime);
                LogScheduledForRemoval(item.Title, futureRemovalTime);
            }
            // If already scheduled, keep existing removal time
        }
        else if (queueRecord.RemoveAt.HasValue)
        {
            // Item is no longer failing, clear scheduled removal
            LogFailureStateReset(item.Title);
            queueRecord.ClearRemoval();
        }
    }

    private bool ShouldMarkAsFailedForItem(IQueueResource item)
    {
        // Check for stalled errors - these always indicate failure
        if (_stalledErrorMessages.Any(msg => item.HasErrorMessage(msg)))
        {
            return true;
        }

        // Allow queued items that are actively downloading to progress
        if (item.Status == QueueStatus.Queued && item.TrackedDownloadState == TrackedDownloadState.Downloading)
        {
            // Only mark as failed if it's been past estimated completion time for too long
            if (item.EstimatedCompletionTime.HasValue)
            {
                TimeSpan timeSinceEstimatedCompletion = timeProvider.GetUtcNow() - item.EstimatedCompletionTime.Value;
                if (timeSinceEstimatedCompletion.TotalHours > _options.MaxDownloadTimeHours)
                {
                    return true;
                }
            }

            // Otherwise allow queued downloading items to continue
            return false;
        }

        // For all other items, check if they're past estimated completion time
        if (!item.EstimatedCompletionTime.HasValue)
        {
            return true;
        }

        TimeSpan duration = timeProvider.GetUtcNow() - item.EstimatedCompletionTime.Value;
        if (duration.TotalHours > _options.MaxDownloadTimeHours)
        {
            return true;
        }

        // Mark as failed if status is Failed, or if Queued but NOT actively downloading
        if (item.Status == QueueStatus.Failed || (item.Status == QueueStatus.Queued && item.TrackedDownloadState != TrackedDownloadState.Downloading))
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
            TimeSpan durationSinceAdded = timeProvider.GetUtcNow() - item.Added.Value;
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

        var queueManager = queueManagers.FirstOrDefault(m => m.SourceName == item.Source);
        if (queueManager is null)
        {
            LogErrorRemovingItemFromQueue(item.Source);
            return false;
        }

        var (shouldRemove, downloadedScore) = await queueManager.ShouldRemoveImmediately(item, cancellationToken);
        if (!shouldRemove)
        {
            return false;
        }

        LogLowerCustomFormatScore(item.Title, item.CustomFormatScore, downloadedScore);
        return true;
    }

    private async IAsyncEnumerable<IQueueResource> GetAllQueueItems([EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        foreach (var queueManager in queueManagers)
        {
            var items = queueManager.GetAllQueueItems(cancellationToken);
            await foreach (var item in items.WithCancellation(cancellationToken))
            {
                yield return item;
            }
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Skipping cleanup for queue item with Title: {Title} added less than an hour ago.")]
    public partial void LogSkippingRecentQueueItem(string? title);

    [LoggerMessage(Level = LogLevel.Information, Message = "Cleaning up queue item with Title: {Title}.")]
    public partial void LogCleaningUpQueueItem(string? title);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Record: {Record}")]
    public partial void LogRecord(IQueueResource record);

    [LoggerMessage(Level = LogLevel.Information, Message = "Download with Title: {Title} is progressing normally. Failure state reset.")]
    public partial void LogFailureStateReset(string? title);

    [LoggerMessage(Level = LogLevel.Information, Message = "Queue item with Title: {Title} has lower custom format score ({CurrentScore}) than previous ({PreviousScore}). Removing.")]
    public partial void LogLowerCustomFormatScore(string? title, int currentScore, int previousScore);

    [LoggerMessage(Level = LogLevel.Information, Message = "Queue item with Title: {Title} (DownloadId: {DownloadId}) no longer in queue. Removing tracked record.")]
    public partial void LogQueueItemRemoved(string? title, string? downloadId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Queue item with Title: {Title} scheduled for removal at {RemoveAt}.")]
    public partial void LogScheduledForRemoval(string? title, DateTimeOffset removeAt);

    [LoggerMessage(Level = LogLevel.Error, Message = "No IQueueManager found for source {Source}. Cannot determine if item should be removed immediately.")]
    public partial void LogErrorRemovingItemFromQueue(RecordSource source);
}
