using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Upgradarr.Application.Options;
using Upgradarr.Data;
using Upgradarr.Data.Interfaces;
using Upgradarr.Domain.Entities;
using Upgradarr.Domain.Enums;
using Upgradarr.Domain.Interfaces;
using Upgradarr.Integrations.Enums;
using Upgradarr.Integrations.Interfaces;
using Upgradarr.Integrations.Models;

[assembly: GenerateMock(typeof(IQueueManager))]
[assembly: GenerateMock(typeof(IOptionsSnapshot<CleanupOptions>))]
[assembly: GenerateMock(typeof(IMigrationState))]

namespace Upgradarr.Application.Services;

public class FakeQueueResource : IQueueResource
{
    public int Id { get; set; }
    public string? DownloadId { get; set; }
    public string? Title { get; set; }
    public DateTimeOffset? Added { get; set; }
    public DateTimeOffset? EstimatedCompletionTime { get; set; }
    public string? OutputPath { get; set; }
    public string? ErrorMessage { get; set; }
    public int CustomFormatScore { get; set; }
    public QueueStatus Status { get; set; }
    public IEnumerable<TrackedDownloadStatusMessage>? StatusMessages { get; init; }
    public RecordSource Source { get; set; }
}

public class CleanupServiceTests
{
    private static readonly CleanupOptions DefaultOptions = new()
    {
        FailedDownloadCleanupHours = 12,
        MaxDownloadTimeHours = 24,
        MaxItemAgeDays = 7,
    };

    private static AppDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString()).Options;
        return new AppDbContext(options);
    }

    private static CleanupService CreateService(
        AppDbContext dbContext,
        IEnumerable<IQueueResource> queueItems,
        CleanupOptions? options = null,
        bool migrationDone = true
    )
    {
        var timeProvider = TimeProvider.System;

        var optionsMock = Mock.Of<IOptionsSnapshot<CleanupOptions>>();
        optionsMock.Value.Returns(options ?? DefaultOptions);

        var queueManager = Mock.Of<IQueueManager>();
        queueManager.SourceName.Returns(RecordSource.Sonarr);

        async IAsyncEnumerable<IQueueResource> GetItems()
        {
            foreach (var item in queueItems)
            {
                yield return item;
            }
            await Task.CompletedTask;
        }

        queueManager.GetAllQueueItems(Any<CancellationToken>()).Returns(GetItems());

        var migrationState = Mock.Of<IMigrationState>();
        migrationState.IsDone.Returns(migrationDone);

        return new CleanupService(
            optionsMock.Object,
            NullLogger<CleanupService>.Instance,
            dbContext,
            timeProvider,
            [queueManager.Object],
            migrationState.Object
        );
    }

    private static FakeQueueResource BuildQueueResource(
        int id,
        string downloadId,
        DateTimeOffset? added = null,
        string? outputPath = null,
        QueueStatus status = QueueStatus.Downloading,
        DateTimeOffset? estimatedCompletionTime = null,
        string? errorMessage = null,
        IEnumerable<TrackedDownloadStatusMessage>? statusMessages = null
    ) =>
        new()
        {
            Id = id,
            DownloadId = downloadId,
            Title = $"Item {downloadId}",
            Added = added ?? TimeProvider.System.GetUtcNow().AddHours(-2),
            OutputPath = outputPath ?? "",
            Status = status,
            EstimatedCompletionTime = estimatedCompletionTime,
            ErrorMessage = errorMessage,
            StatusMessages = statusMessages ?? [],
        };

    [Test]
    public async Task PerformCleanupAsync_WithStalledDownload_MarksForRemoval()
    {
        await using var dbContext = CreateDbContext();
        var resource = BuildQueueResource(1, "down1", estimatedCompletionTime: null);
        var service = CreateService(dbContext, [resource]);

        await service.PerformCleanupAsync();

        var tracked = await dbContext.TrackedDownloads.FirstAsync();
        await Assert.That(tracked.RemoveAt).IsNotNull();
    }

    [Test]
    public async Task PerformCleanupAsync_ImmediateRemoval_IfWeirdExtension()
    {
        await using var dbContext = CreateDbContext();
        var resource = BuildQueueResource(2, "down2", outputPath: "/downloads/item.exe");
        var service = CreateService(dbContext, [resource]);

        await service.PerformCleanupAsync();

        var tracked = await dbContext.TrackedDownloads.FirstAsync();
        await Assert.That(tracked.RemoveAt).IsNotNull();
    }

    [Test]
    public async Task PerformCleanupAsync_IgnoresRecentPendingItem()
    {
        await using var dbContext = CreateDbContext();
        var resource = BuildQueueResource(4, "down_recent", added: TimeProvider.System.GetUtcNow().AddMinutes(-30), status: QueueStatus.Downloading);
        var service = CreateService(dbContext, [resource]);

        await service.PerformCleanupAsync();

        var tracked = await dbContext.TrackedDownloads.FirstAsync();
        await Assert.That(tracked.RemoveAt).IsNull();
    }

    [Test]
    public async Task PerformCleanupAsync_RemovesCompletedDownload()
    {
        await using var dbContext = CreateDbContext();
        var resource = BuildQueueResource(5, "down_completed", status: QueueStatus.Completed);
        var service = CreateService(dbContext, [resource]);

        await service.PerformCleanupAsync();

        var tracked = await dbContext.TrackedDownloads.FirstAsync();
        await Assert.That(tracked.RemoveAt).IsNotNull();
    }

    [Test]
    public async Task PerformCleanupAsync_RemovesItemWhenNotFoundInQueueAnymore()
    {
        await using var dbContext = CreateDbContext();
        dbContext.TrackedDownloads.Add(
            new QueueRecord
            {
                DownloadId = "down_missing",
                Source = RecordSource.Sonarr,
                Added = TimeProvider.System.GetUtcNow().AddDays(-1),
                Title = "Missing Item",
            }
        );
        await dbContext.SaveChangesAsync();

        var service = CreateService(dbContext, []);

        await service.PerformCleanupAsync();

        var count = await dbContext.TrackedDownloads.CountAsync();
        await Assert.That(count).IsEqualTo(0);
    }

    [Test]
    public async Task PerformCleanupAsync_DoesNothing_WhenMigrationNotDone()
    {
        await using var dbContext = CreateDbContext();
        var resource = BuildQueueResource(1, "down1");
        var service = CreateService(dbContext, [resource], migrationDone: false);

        await service.PerformCleanupAsync();

        var count = await dbContext.TrackedDownloads.CountAsync();
        await Assert.That(count).IsEqualTo(0);
    }

    [Test]
    public async Task PerformCleanupAsync_ImmediateRemoval_WhenItemExceedsMaxAge()
    {
        await using var dbContext = CreateDbContext();
        // Added 8 days ago, MaxItemAgeDays=7
        var resource = BuildQueueResource(1, "down_old", added: TimeProvider.System.GetUtcNow().AddDays(-8));
        var service = CreateService(dbContext, [resource]);

        await service.PerformCleanupAsync();

        var tracked = await dbContext.TrackedDownloads.FirstAsync();
        await Assert.That(tracked.RemoveAt).IsNotNull();
    }

    [Test]
    public async Task PerformCleanupAsync_ImmediateRemoval_WhenErrorMessageMatchesImmediateList()
    {
        await using var dbContext = CreateDbContext();
        var resource = BuildQueueResource(1, "down_invalid", errorMessage: "Invalid video file");
        var service = CreateService(dbContext, [resource]);

        await service.PerformCleanupAsync();

        var tracked = await dbContext.TrackedDownloads.FirstAsync();
        await Assert.That(tracked.RemoveAt).IsNotNull();
    }

    [Test]
    public async Task PerformCleanupAsync_ImmediateRemoval_WhenStalledMessageInStatusMessages()
    {
        await using var dbContext = CreateDbContext();
        var resource = BuildQueueResource(
            1,
            "down_stalled_msg",
            statusMessages: [new TrackedDownloadStatusMessage { Title = "Warning", Messages = ["The download is stalled with no connections"] }]
        );
        // Give a future ETA so "no ETA" path isn't triggered first
        resource.EstimatedCompletionTime = TimeProvider.System.GetUtcNow().AddHours(1);
        var service = CreateService(dbContext, [resource]);

        await service.PerformCleanupAsync();

        var tracked = await dbContext.TrackedDownloads.FirstAsync();
        // "stalled" message is in _stalledErrorMessages → ShouldMarkAsFailedForItem = true → scheduled (not immediate via ShouldRemoveImmediatelyAsync)
        await Assert.That(tracked.RemoveAt).IsNotNull();
    }

    [Test]
    public async Task PerformCleanupAsync_SchedulesRemoval_WhenStatusIsFailed()
    {
        await using var dbContext = CreateDbContext();
        // Future ETA so the missing-ETA path isn't triggered
        var resource = BuildQueueResource(1, "down_failed", status: QueueStatus.Failed, estimatedCompletionTime: TimeProvider.System.GetUtcNow().AddHours(1));
        var service = CreateService(dbContext, [resource]);

        await service.PerformCleanupAsync();

        var tracked = await dbContext.TrackedDownloads.FirstAsync();
        await Assert.That(tracked.RemoveAt).IsNotNull();
    }

    [Test]
    public async Task PerformCleanupAsync_SchedulesRemoval_WhenStatusIsQueued()
    {
        await using var dbContext = CreateDbContext();
        var resource = BuildQueueResource(1, "down_queued", status: QueueStatus.Queued, estimatedCompletionTime: TimeProvider.System.GetUtcNow().AddHours(1));
        var service = CreateService(dbContext, [resource]);

        await service.PerformCleanupAsync();

        var tracked = await dbContext.TrackedDownloads.FirstAsync();
        await Assert.That(tracked.RemoveAt).IsNotNull();
    }

    [Test]
    public async Task PerformCleanupAsync_SchedulesRemoval_WhenExceedsMaxDownloadTime()
    {
        await using var dbContext = CreateDbContext();
        // ETA was 25 hours ago (MaxDownloadTimeHours=24)
        var resource = BuildQueueResource(
            1,
            "down_overdue",
            status: QueueStatus.Downloading,
            estimatedCompletionTime: TimeProvider.System.GetUtcNow().AddHours(-25)
        );
        var service = CreateService(dbContext, [resource]);

        await service.PerformCleanupAsync();

        var tracked = await dbContext.TrackedDownloads.FirstAsync();
        await Assert.That(tracked.RemoveAt).IsNotNull();
    }

    [Test]
    public async Task PerformCleanupAsync_KeepsExistingRemovalTime_WhenAlreadyScheduled()
    {
        await using var dbContext = CreateDbContext();
        var originalRemoveAt = TimeProvider.System.GetUtcNow().AddHours(6);

        var existingRecord = new QueueRecord
        {
            DownloadId = "down_scheduled",
            Title = "Scheduled Item",
            Added = TimeProvider.System.GetUtcNow().AddHours(-3),
            Source = RecordSource.Sonarr,
        };
        existingRecord.MarkForRemoval(originalRemoveAt);
        dbContext.TrackedDownloads.Add(existingRecord);
        await dbContext.SaveChangesAsync();

        // Item is still stalled (no ETA) → ShouldMarkAsFailedForItem = true, but already scheduled
        var resource = BuildQueueResource(1, "down_scheduled", estimatedCompletionTime: null);
        var service = CreateService(dbContext, [resource]);

        await service.PerformCleanupAsync();

        var tracked = await dbContext.TrackedDownloads.FirstAsync();
        await Assert.That(tracked.RemoveAt).IsEqualTo(originalRemoveAt);
    }

    [Test]
    public async Task PerformCleanupAsync_ClearsScheduledRemoval_WhenItemIsProgressing()
    {
        await using var dbContext = CreateDbContext();

        var existingRecord = new QueueRecord
        {
            DownloadId = "down_recovering",
            Title = "Recovering Item",
            Added = TimeProvider.System.GetUtcNow().AddHours(-3),
            Source = RecordSource.Sonarr,
        };
        existingRecord.MarkForRemoval(TimeProvider.System.GetUtcNow().AddHours(5));
        dbContext.TrackedDownloads.Add(existingRecord);
        await dbContext.SaveChangesAsync();

        // Item now has a healthy future ETA, Downloading status – no longer failing
        var resource = BuildQueueResource(
            1,
            "down_recovering",
            status: QueueStatus.Downloading,
            estimatedCompletionTime: TimeProvider.System.GetUtcNow().AddHours(2)
        );
        var service = CreateService(dbContext, [resource]);

        await service.PerformCleanupAsync();

        var tracked = await dbContext.TrackedDownloads.FirstAsync();
        await Assert.That(tracked.RemoveAt).IsNull();
    }
}
