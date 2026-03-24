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
}
