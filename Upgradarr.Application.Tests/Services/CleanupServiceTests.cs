using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Upgradarr.Application.Options;
using Upgradarr.Application.Services;
using Upgradarr.Data;
using Upgradarr.Domain.Enums;
using Upgradarr.Domain.Interfaces;
using Upgradarr.Domain.ValueObjects;

[assembly: GenerateMock(typeof(IQueueManager))]
[assembly: GenerateMock(typeof(IOptionsSnapshot<CleanupOptions>))]

namespace Upgradarr.Application.Tests.Services;

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
    private static AppDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString()).Options;
        return new AppDbContext(options);
    }

    [Test]
    public async Task PerformCleanupAsync_WithStalledDownload_MarksForRemoval()
    {
        // Arrange
        await using var dbContext = CreateDbContext();
        var timeProvider = TimeProvider.System;
        var logger = NullLogger<CleanupService>.Instance;

        var optionsMock = Mock.Of<IOptionsSnapshot<CleanupOptions>>();
        optionsMock.Value.Returns(
            new CleanupOptions
            {
                FailedDownloadCleanupHours = 12,
                MaxDownloadTimeHours = 24,
                MaxItemAgeDays = 7,
            }
        );

        var queueResource = new FakeQueueResource
        {
            DownloadId = "down1",
            Title = "Stalled Item",
            Added = timeProvider.GetUtcNow().AddHours(-2),
            EstimatedCompletionTime = null,
            Id = 1,
            OutputPath = "",
            StatusMessages = [],
        };

        var queueManager = Mock.Of<IQueueManager>();
        queueManager.SourceName.Returns(RecordSource.Sonarr);

        async IAsyncEnumerable<IQueueResource> GetItems()
        {
            yield return queueResource;
            await Task.CompletedTask;
        }

        queueManager.GetAllQueueItems(Any<CancellationToken>()).Returns(GetItems());

        var service = new CleanupService(optionsMock.Object, logger, dbContext, timeProvider, [queueManager.Object]);

        // Act
        await service.PerformCleanupAsync();

        // Assert
        var tracked = await dbContext.TrackedDownloads.FirstAsync();
        await Assert.That(tracked.RemoveAt).IsNotNull();
    }

    [Test]
    public async Task PerformCleanupAsync_ImmediateRemoval_IfWeirdExtension()
    {
        await using var dbContext = CreateDbContext();
        var timeProvider = TimeProvider.System;
        var logger = NullLogger<CleanupService>.Instance;

        var optionsMock = Mock.Of<IOptionsSnapshot<CleanupOptions>>();
        optionsMock.Value.Returns(
            new CleanupOptions
            {
                FailedDownloadCleanupHours = 12,
                MaxDownloadTimeHours = 24,
                MaxItemAgeDays = 7,
            }
        );

        var queueResource = new FakeQueueResource
        {
            DownloadId = "down2",
            Title = "Weird Item",
            Added = timeProvider.GetUtcNow().AddHours(-2),
            OutputPath = "/downloads/item.exe",
            Id = 2,
            StatusMessages = [],
        };

        var queueManager = Mock.Of<IQueueManager>();
        queueManager.SourceName.Returns(RecordSource.Sonarr);

        async IAsyncEnumerable<IQueueResource> GetItems()
        {
            yield return queueResource;
            await Task.CompletedTask;
        }
        queueManager.GetAllQueueItems(Any<CancellationToken>()).Returns(GetItems());

        var service = new CleanupService(optionsMock.Object, logger, dbContext, timeProvider, [queueManager.Object]);

        await service.PerformCleanupAsync();

        var tracked = await dbContext.TrackedDownloads.FirstAsync();
        // Since it's immediate, it marks for removal exactly at timeProvider.GetUtcNow()
        await Assert.That(tracked.RemoveAt).IsNotNull();
    }
}
