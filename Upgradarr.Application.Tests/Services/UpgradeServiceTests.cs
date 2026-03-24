using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;
using TUnit.Mocks;
using Upgradarr.Application.Services;
using Upgradarr.Data;
using Upgradarr.Domain.Entities;
using Upgradarr.Domain.Enums;
using Upgradarr.Domain.Interfaces;
using static TUnit.Mocks.Arguments.Arg;

[assembly: GenerateMock(typeof(IUpgradeManager))]

namespace Upgradarr.Application.Tests.Services;

public class UpgradeServiceTests
{
    private static AppDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString()).Options;
        return new AppDbContext(options);
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
            .Returns([
                new UpgradeState
                {
                    ItemType = ItemType.Series,
                    ItemId = 1,
                    SearchState = SearchState.Pending,
                    IsMonitored = true,
                },
            ]);

        var manager2 = Mock.Of<IUpgradeManager>();
        manager2
            .BuildQueueItemsAsync(Any<CancellationToken>())
            .Returns([
                new UpgradeState
                {
                    ItemType = ItemType.Movie,
                    ItemId = 2,
                    SearchState = SearchState.Pending,
                    IsMonitored = true,
                },
            ]);

        var service = new UpgradeService([manager1.Object, manager2.Object], dbContext, logger, timeProvider);

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

        var service = new UpgradeService([manager1.Object, manager2.Object], dbContext, logger, timeProvider);

        // Act
        await service.ProcessItemUpgradeAsync(state);

        // Assert
        Mock.VerifyAll(manager1);
        Mock.VerifyAll(manager2);

        var updatedState = await dbContext.UpgradeStates.FirstAsync();
        await Assert.That(updatedState.SearchState).IsEqualTo(SearchState.Searched);
    }
}
