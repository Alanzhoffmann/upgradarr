using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Upgradarr.Data;
using Upgradarr.Data.Interfaces;
using Upgradarr.Domain.Entities;
using Upgradarr.Domain.Enums;
using Upgradarr.Domain.ValueObjects;
using Upgradarr.Integrations.Interfaces;

[assembly: GenerateMock(typeof(IUpgradeManager))]
[assembly: GenerateMock(typeof(IMigrationState))]

namespace Upgradarr.Application.Services;

public class UpgradeServiceTests
{
    private static AppDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString()).Options;
        return new AppDbContext(options);
    }

    private static UpgradeService CreateService(AppDbContext dbContext, IEnumerable<IUpgradeManager> managers, bool migrationDone = true)
    {
        var migrationState = Mock.Of<IMigrationState>();
        migrationState.IsDone.Returns(migrationDone);
        return new UpgradeService(managers, dbContext, NullLogger<UpgradeService>.Instance, TimeProvider.System, migrationState.Object);
    }

    private static async IAsyncEnumerable<UpgradeState> AsAsyncEnumerable(params IEnumerable<UpgradeState> states)
    {
        foreach (var state in states)
        {
            yield return state;
        }
    }

    private static async IAsyncEnumerable<UpgradeState> EmptyBuildItems()
    {
        await Task.CompletedTask;
        yield break;
    }

    [Test]
    public async Task ProcessItemUpgradeAsync_DelegatesToCorrectManager()
    {
        // Arrange
        await using var dbContext = CreateDbContext();

        var state = new UpgradeState
        {
            ItemId = 1,
            ItemType = ItemType.Movie,
            SearchState = SearchState.Pending,
        };
        dbContext.UpgradeStates.Add(state);
        await dbContext.SaveChangesAsync();

        var manager1 = Mock.Of<IUpgradeManager>();
        manager1.CanHandle(ItemType.Movie).Returns(false);

        var manager2 = Mock.Of<IUpgradeManager>();
        manager2.CanHandle(ItemType.Movie).Returns(true);
        manager2.ProcessUpgradeAsync(state, Any<CancellationToken>()).Returns(UpgradeActionResult.Searched);

        var service = CreateService(dbContext, [manager1.Object, manager2.Object]);

        // Act
        await service.ProcessItemUpgradeAsync(state);

        // Assert
        manager1.VerifyAll();
        manager2.VerifyAll();

        var updatedState = await dbContext.UpgradeStates.FirstAsync();
        await Assert.That(updatedState.SearchState).IsEqualTo(SearchState.Searched);
    }

    [Test]
    public async Task ProcessItemUpgradeAsync_RemovesWhenActionIsRemoved()
    {
        await using var dbContext = CreateDbContext();

        var state = new UpgradeState
        {
            ItemId = 2,
            ItemType = ItemType.Series,
            SearchState = SearchState.Pending,
        };
        dbContext.UpgradeStates.Add(state);
        await dbContext.SaveChangesAsync();

        var manager = Mock.Of<IUpgradeManager>();
        manager.CanHandle(ItemType.Series).Returns(true);
        manager.ProcessUpgradeAsync(state, Any<CancellationToken>()).Returns(UpgradeActionResult.Removed);

        var service = CreateService(dbContext, [manager.Object]);

        await service.ProcessItemUpgradeAsync(state);

        var count = await dbContext.UpgradeStates.CountAsync();
        await Assert.That(count).IsEqualTo(0);
    }

    [Test]
    public async Task ProcessItemUpgradeAsync_HandlesExceptionGracefully()
    {
        await using var dbContext = CreateDbContext();

        var state = new UpgradeState
        {
            ItemId = 3,
            ItemType = ItemType.Episode,
            SearchState = SearchState.Pending,
        };
        dbContext.UpgradeStates.Add(state);
        await dbContext.SaveChangesAsync();

        var manager = Mock.Of<IUpgradeManager>();
        manager.CanHandle(ItemType.Episode).Returns(true);
        manager.ProcessUpgradeAsync(state, Any<CancellationToken>()).Throws(new Exception("API failure"));

        var service = CreateService(dbContext, [manager.Object]);

        await service.ProcessItemUpgradeAsync(state);

        var updatedState = await dbContext.UpgradeStates.FirstAsync();
        await Assert.That(updatedState.SearchState).IsEqualTo(SearchState.Pending);
    }

    [Test]
    public async Task ProcessItemUpgradeAsync_ReturnsFalse_WhenStateIsNull()
    {
        await using var dbContext = CreateDbContext();
        var service = CreateService(dbContext, []);

        var result = await service.ProcessItemUpgradeAsync(null);

        await Assert.That(result).IsFalse();
    }

    [Test]
    public async Task ProcessItemUpgradeAsync_ReturnsFalse_WhenNoMatchingManager()
    {
        await using var dbContext = CreateDbContext();

        var state = new UpgradeState
        {
            ItemId = 10,
            ItemType = ItemType.Movie,
            SearchState = SearchState.Pending,
        };
        dbContext.UpgradeStates.Add(state);
        await dbContext.SaveChangesAsync();

        var manager = Mock.Of<IUpgradeManager>();
        manager.CanHandle(ItemType.Movie).Returns(false);

        var service = CreateService(dbContext, [manager.Object]);

        var result = await service.ProcessItemUpgradeAsync(state);

        await Assert.That(result).IsFalse();
        var unchanged = await dbContext.UpgradeStates.FirstAsync();
        await Assert.That(unchanged.SearchState).IsEqualTo(SearchState.Pending);
    }

    [Test]
    public async Task ProcessItemUpgradeAsync_ReturnsFalse_WhenResultIsSkipped()
    {
        await using var dbContext = CreateDbContext();

        var state = new UpgradeState
        {
            ItemId = 11,
            ItemType = ItemType.Movie,
            SearchState = SearchState.Pending,
        };
        dbContext.UpgradeStates.Add(state);
        await dbContext.SaveChangesAsync();

        var manager = Mock.Of<IUpgradeManager>();
        manager.CanHandle(ItemType.Movie).Returns(true);
        manager.ProcessUpgradeAsync(state, Any<CancellationToken>()).Returns(UpgradeActionResult.Skipped);

        var service = CreateService(dbContext, [manager.Object]);

        var result = await service.ProcessItemUpgradeAsync(state);

        await Assert.That(result).IsFalse();
        var unchanged = await dbContext.UpgradeStates.FirstAsync();
        await Assert.That(unchanged.SearchState).IsEqualTo(SearchState.Pending);
    }

    [Test]
    public async Task ProcessItemUpgradeAsync_RemovesSeriesAndChildren_WhenActionIsRemoved()
    {
        await using var dbContext = CreateDbContext();

        var series = new UpgradeState
        {
            ItemId = 100,
            ItemType = ItemType.Series,
            SearchState = SearchState.Pending,
        };
        var season = new UpgradeState
        {
            ItemId = 200,
            ItemType = ItemType.Season,
            ParentSeriesId = 100,
            SeasonNumber = 1,
            SearchState = SearchState.Pending,
        };
        var episode = new UpgradeState
        {
            ItemId = 300,
            ItemType = ItemType.Episode,
            ParentSeriesId = 100,
            SeasonNumber = 1,
            EpisodeNumber = 1,
            SearchState = SearchState.Pending,
        };
        dbContext.UpgradeStates.AddRange(series, season, episode);
        await dbContext.SaveChangesAsync();

        var manager = Mock.Of<IUpgradeManager>();
        manager.CanHandle(ItemType.Series).Returns(true);
        manager.ProcessUpgradeAsync(series, Any<CancellationToken>()).Returns(UpgradeActionResult.Removed);

        var service = CreateService(dbContext, [manager.Object]);

        await service.ProcessItemUpgradeAsync(series);

        var count = await dbContext.UpgradeStates.CountAsync();
        await Assert.That(count).IsEqualTo(0);
    }

    [Test]
    public async Task ProcessItemUpgradeAsync_RemovesSeasonAndEpisodes_WhenActionIsRemoved()
    {
        await using var dbContext = CreateDbContext();

        var season = new UpgradeState
        {
            ItemId = 200,
            ItemType = ItemType.Season,
            ParentSeriesId = 100,
            SeasonNumber = 1,
            SearchState = SearchState.Pending,
        };
        var episode1 = new UpgradeState
        {
            ItemId = 301,
            ItemType = ItemType.Episode,
            ParentSeriesId = 100,
            SeasonNumber = 1,
            EpisodeNumber = 1,
            SearchState = SearchState.Pending,
        };
        // Episode from a different season – should NOT be removed
        var otherSeasonEpisode = new UpgradeState
        {
            ItemId = 401,
            ItemType = ItemType.Episode,
            ParentSeriesId = 100,
            SeasonNumber = 2,
            EpisodeNumber = 1,
            SearchState = SearchState.Pending,
        };
        dbContext.UpgradeStates.AddRange(season, episode1, otherSeasonEpisode);
        await dbContext.SaveChangesAsync();

        var manager = Mock.Of<IUpgradeManager>();
        manager.CanHandle(ItemType.Season).Returns(true);
        manager.ProcessUpgradeAsync(season, Any<CancellationToken>()).Returns(UpgradeActionResult.Removed);

        var service = CreateService(dbContext, [manager.Object]);

        await service.ProcessItemUpgradeAsync(season);

        var remaining = await dbContext.UpgradeStates.ToListAsync();
        await Assert.That(remaining.Count).IsEqualTo(1);
        await Assert.That(remaining[0].SeasonNumber).IsEqualTo(2);
    }

    [Test]
    public async Task ProcessUpgradeAsync_DoesNothing_WhenMigrationNotDone()
    {
        await using var dbContext = CreateDbContext();

        var state = new UpgradeState
        {
            ItemId = 1,
            ItemType = ItemType.Movie,
            SearchState = SearchState.Pending,
        };
        dbContext.UpgradeStates.Add(state);
        await dbContext.SaveChangesAsync();

        var manager = Mock.Of<IUpgradeManager>();
        var service = CreateService(dbContext, [manager.Object], migrationDone: false);

        await service.ProcessUpgradeAsync();

        var unchanged = await dbContext.UpgradeStates.FirstAsync();
        await Assert.That(unchanged.SearchState).IsEqualTo(SearchState.Pending);
    }

    [Test]
    public async Task ProcessUpgradeAsync_SearchesPendingItem()
    {
        await using var dbContext = CreateDbContext();

        var state = new UpgradeState
        {
            ItemId = 1,
            ItemType = ItemType.Movie,
            SearchState = SearchState.Pending,
            IsMonitored = true,
        };
        dbContext.UpgradeStates.Add(state);
        await dbContext.SaveChangesAsync();

        var manager = Mock.Of<IUpgradeManager>();
        manager.CanHandle(ItemType.Movie).Returns(true);
        manager.HasOngoingDownloadAsync(Any<UpgradeState>(), Any<CancellationToken>()).Returns(false);
        manager.ProcessUpgradeAsync(Any<UpgradeState>(), Any<CancellationToken>()).Returns(UpgradeActionResult.Searched);

        // Return matching item so IsMonitored stays true in AddNewItemsToQueueAsync
        async IAsyncEnumerable<UpgradeState> GetBuildItems()
        {
            yield return new UpgradeState
            {
                ItemId = 1,
                ItemType = ItemType.Movie,
                SearchState = SearchState.Pending,
            };
            await Task.CompletedTask;
        }
        manager.BuildQueueItemsAsync(Any<CancellationToken>()).Returns(GetBuildItems());

        var service = CreateService(dbContext, [manager.Object]);

        await service.ProcessUpgradeAsync();

        var updated = await dbContext.UpgradeStates.FirstAsync();
        await Assert.That(updated.SearchState).IsEqualTo(SearchState.Searched);
    }

    [Test]
    public async Task ProcessUpgradeAsync_ReturnsImmediately_WhenNoPendingItems()
    {
        await using var dbContext = CreateDbContext();

        var manager = Mock.Of<IUpgradeManager>();
        manager.BuildQueueItemsAsync(Any<CancellationToken>()).Returns(EmptyBuildItems());

        var service = CreateService(dbContext, [manager.Object]);

        // Should complete without throwing
        await service.ProcessUpgradeAsync();

        var count = await dbContext.UpgradeStates.CountAsync();
        await Assert.That(count).IsEqualTo(0);
    }

    [Test]
    public async Task AddItemsToFrontOfQueueAsync_ShiftsExistingItemsAndResetsPendingState()
    {
        await using var dbContext = CreateDbContext();

        var item1 = new UpgradeState
        {
            ItemId = 1,
            ItemType = ItemType.Movie,
            SearchState = SearchState.Searched,
            QueuePosition = 0,
        };
        var item2 = new UpgradeState
        {
            ItemId = 2,
            ItemType = ItemType.Movie,
            SearchState = SearchState.Pending,
            QueuePosition = 1,
        };
        dbContext.UpgradeStates.AddRange(item1, item2);
        await dbContext.SaveChangesAsync();

        var service = CreateService(dbContext, []);

        await service.AddItemsToFrontOfQueueAsync([new ItemToQueue(ItemType.Movie, 1)]);

        var updated1 = await dbContext.UpgradeStates.FirstAsync(u => u.ItemId == 1);
        var updated2 = await dbContext.UpgradeStates.FirstAsync(u => u.ItemId == 2);

        await Assert.That(updated1.SearchState).IsEqualTo(SearchState.Pending);
        await Assert.That(updated1.QueuePosition).IsEqualTo(0);
        await Assert.That(updated2.QueuePosition).IsEqualTo(2);
    }

    [Test]
    public async Task AddItemsToFrontOfQueueAsync_DoesNothing_ForItemNotInQueue()
    {
        await using var dbContext = CreateDbContext();

        var item = new UpgradeState
        {
            ItemId = 1,
            ItemType = ItemType.Movie,
            SearchState = SearchState.Pending,
            QueuePosition = 0,
        };
        dbContext.UpgradeStates.Add(item);
        await dbContext.SaveChangesAsync();

        var service = CreateService(dbContext, []);

        // ItemId 99 does not exist in the queue
        await service.AddItemsToFrontOfQueueAsync([new ItemToQueue(ItemType.Movie, 99)]);

        var unchanged = await dbContext.UpgradeStates.FirstAsync();
        await Assert.That(unchanged.QueuePosition).IsEqualTo(1); // shifted by 1 (items.Count)
        await Assert.That(unchanged.SearchState).IsEqualTo(SearchState.Pending);
    }

    [Test]
    public async Task ProcessUpgradeAsync_ResetsQueue_WhenAllItemsAreSearched()
    {
        await using var dbContext = CreateDbContext();

        // Only a Searched item – no pending work
        var state = new UpgradeState
        {
            ItemId = 1,
            ItemType = ItemType.Movie,
            SearchState = SearchState.Searched,
            IsMonitored = true,
        };
        dbContext.UpgradeStates.Add(state);
        await dbContext.SaveChangesAsync();

        var manager = Mock.Of<IUpgradeManager>();
        manager.CanHandle(ItemType.Movie).Returns(true);
        // Return empty so that AddNewItemsToQueueAsync marks existing item as unmonitored,
        // then GetNextItemToUpgradeAsync detects searched items and calls ResetQueueAsync.
        manager.BuildQueueItemsAsync(Any<CancellationToken>()).Returns(EmptyBuildItems());

        var service = CreateService(dbContext, [manager.Object]);

        await service.ProcessUpgradeAsync();

        // ResetQueueAsync cleared the DB (BuildQueueItemsAsync returned empty)
        var count = await dbContext.UpgradeStates.CountAsync();
        await Assert.That(count).IsEqualTo(0);
    }

    [Test]
    public async Task ProcessUpgradeAsync_AddsNewItemsFromBuildQueue_BeforeProcessing()
    {
        await using var dbContext = CreateDbContext();

        var manager = Mock.Of<IUpgradeManager>();
        manager.CanHandle(ItemType.Movie).Returns(true);
        manager.HasOngoingDownloadAsync(Any<UpgradeState>(), Any<CancellationToken>()).Returns(false);
        manager.ProcessUpgradeAsync(Any<UpgradeState>(), Any<CancellationToken>()).Returns(UpgradeActionResult.Searched);

        // BuildQueueItemsAsync provides a brand-new item not yet in DB
        async IAsyncEnumerable<UpgradeState> GetBuildItems()
        {
            yield return new UpgradeState
            {
                ItemId = 1,
                ItemType = ItemType.Movie,
                SearchState = SearchState.Pending,
                IsMonitored = true,
            };
            await Task.CompletedTask;
        }
        manager.BuildQueueItemsAsync(Any<CancellationToken>()).Returns(GetBuildItems());

        var service = CreateService(dbContext, [manager.Object]);

        // DB starts empty – item is discovered via BuildQueueItemsAsync
        await service.ProcessUpgradeAsync();

        var updated = await dbContext.UpgradeStates.FirstAsync();
        await Assert.That(updated.SearchState).IsEqualTo(SearchState.Searched);
    }
}
