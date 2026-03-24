using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Upgradarr.Application.Services;
using Upgradarr.Data;
using Upgradarr.Data.Interfaces;
using Upgradarr.Domain.Entities;
using Upgradarr.Domain.Enums;
using Upgradarr.Domain.Interfaces;

[assembly: GenerateMock(typeof(IUpgradeManager))]
[assembly: GenerateMock(typeof(IMigrationState))]

namespace Upgradarr.Application.Tests.Services;

public class UpgradeServiceTests
{
    private static AppDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString()).Options;
        return new AppDbContext(options);
    }

    private static async IAsyncEnumerable<UpgradeState> AsAsyncEnumerable(params IEnumerable<UpgradeState> states)
    {
        foreach (var state in states)
        {
            yield return state;
        }
    }

    [Test]
    public async Task InitializeUpgradeStatesAsync_CallsAllManagersAndSavesToDb()
    {
        // Arrange
        await using var dbContext = CreateDbContext();
        var timeProvider = TimeProvider.System;
        var logger = NullLogger<UpgradeService>.Instance;

        var manager1 = Mock.Of<IUpgradeManager>();
        manager1
            .BuildQueueItemsAsync(Any<CancellationToken>())
            .Returns(
                AsAsyncEnumerable(
                    new UpgradeState
                    {
                        ItemType = ItemType.Series,
                        ItemId = 1,
                        SearchState = SearchState.Pending,
                        IsMonitored = true,
                    }
                )
            );

        var manager2 = Mock.Of<IUpgradeManager>();
        manager2
            .BuildQueueItemsAsync(Any<CancellationToken>())
            .Returns(
                AsAsyncEnumerable(
                    new UpgradeState
                    {
                        ItemType = ItemType.Movie,
                        ItemId = 2,
                        SearchState = SearchState.Pending,
                        IsMonitored = true,
                    }
                )
            );

        var migrationState = Mock.Of<IMigrationState>();
        migrationState.IsDone.Returns(true);

        var service = new UpgradeService([manager1.Object, manager2.Object], dbContext, logger, timeProvider, migrationState.Object);

        // Act
        await service.InitializeUpgradeStatesAsync();

        // Assert
        var states = await dbContext.UpgradeStates.ToListAsync();
        await Assert.That(states.Count).IsEqualTo(2);
        await Assert.That(states.Any(s => s.ItemType == ItemType.Series && s.ItemId == 1)).IsTrue();
        await Assert.That(states.Any(s => s.ItemType == ItemType.Movie && s.ItemId == 2)).IsTrue();
    }

    [Test]
    public async Task ProcessItemUpgradeAsync_DelegatesToCorrectManager()
    {
        // Arrange
        await using var dbContext = CreateDbContext();
        var timeProvider = TimeProvider.System;
        var logger = NullLogger<UpgradeService>.Instance;

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

        var migrationState = Mock.Of<IMigrationState>();
        migrationState.IsDone.Returns(true);

        var service = new UpgradeService([manager1.Object, manager2.Object], dbContext, logger, timeProvider, migrationState.Object);

        // Act
        await service.ProcessItemUpgradeAsync(state);

        // Assert
        Mock.VerifyAll(manager1);
        Mock.VerifyAll(manager2);

        var updatedState = await dbContext.UpgradeStates.FirstAsync();
        await Assert.That(updatedState.SearchState).IsEqualTo(SearchState.Searched);
    }

    [Test]
    public async Task ProcessItemUpgradeAsync_RemovesWhenActionIsRemoved()
    {
        await using var dbContext = CreateDbContext();
        var timeProvider = TimeProvider.System;
        var logger = NullLogger<UpgradeService>.Instance;

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

        var migrationState = Mock.Of<IMigrationState>();
        migrationState.IsDone.Returns(true);

        var service = new UpgradeService([manager.Object], dbContext, logger, timeProvider, migrationState.Object);

        await service.ProcessItemUpgradeAsync(state);

        var count = await dbContext.UpgradeStates.CountAsync();
        await Assert.That(count).IsEqualTo(0);
    }

    [Test]
    public async Task ProcessItemUpgradeAsync_HandlesExceptionGracefully()
    {
        await using var dbContext = CreateDbContext();
        var timeProvider = TimeProvider.System;
        var logger = NullLogger<UpgradeService>.Instance;

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

        var migrationState = Mock.Of<IMigrationState>();
        migrationState.IsDone.Returns(true);

        var service = new UpgradeService([manager.Object], dbContext, logger, timeProvider, migrationState.Object);

        await service.ProcessItemUpgradeAsync(state);

        var updatedState = await dbContext.UpgradeStates.FirstAsync();
        await Assert.That(updatedState.SearchState).IsEqualTo(SearchState.Pending);
    }

    [Test]
    public async Task InitializeUpgradeStatesAsync_AbortsIfQueueAlreadyPopulated()
    {
        await using var dbContext = CreateDbContext();
        var timeProvider = TimeProvider.System;
        var logger = NullLogger<UpgradeService>.Instance;

        dbContext.UpgradeStates.Add(
            new UpgradeState
            {
                ItemId = 10,
                ItemType = ItemType.Movie,
                SearchState = SearchState.Pending,
            }
        );
        await dbContext.SaveChangesAsync();

        var manager = Mock.Of<IUpgradeManager>();
        manager
            .BuildQueueItemsAsync(Any<CancellationToken>())
            .Returns(
                AsAsyncEnumerable(
                    new UpgradeState
                    {
                        ItemId = 10,
                        ItemType = ItemType.Movie,
                        SearchState = SearchState.Pending,
                    },
                    new UpgradeState
                    {
                        ItemId = 11,
                        ItemType = ItemType.Movie,
                        SearchState = SearchState.Pending,
                    }
                )
            );

        var migrationState = Mock.Of<IMigrationState>();
        migrationState.IsDone.Returns(true);

        var service = new UpgradeService([manager.Object], dbContext, logger, timeProvider, migrationState.Object);

        await service.InitializeUpgradeStatesAsync();

        var states = await dbContext.UpgradeStates.ToListAsync();
        await Assert.That(states.Count).IsEqualTo(1);
    }
}
