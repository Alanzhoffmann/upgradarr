using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Upgradarr.Data;
using Upgradarr.Data.Interfaces;
using Upgradarr.Domain.Entities;
using Upgradarr.Domain.Entities.Radarr;
using Upgradarr.Domain.Entities.Sonarr;
using Upgradarr.Domain.Enums;
using Upgradarr.Domain.Interfaces;

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

    private static async IAsyncEnumerable<UpgradeState> AsAsyncEnumerable(params IEnumerable<UpgradeState> states)
    {
        foreach (var state in states)
        {
            yield return state;
        }
    }

    [Test]
    public async Task ProcessItemUpgradeAsync_DelegatesToCorrectManager()
    {
        // Arrange
        await using var dbContext = CreateDbContext();
        var timeProvider = TimeProvider.System;
        var logger = NullLogger<UpgradeService>.Instance;

        var state = new RadarrUpgradeState
        {
            ItemId = 1,
            SearchState = SearchState.Pending,
            Metadata = new RadarrMetadata(RadarrItemType.Movie),
        };
        dbContext.UpgradeStates.Add(state);
        await dbContext.SaveChangesAsync();

        var manager1 = Mock.Of<IUpgradeManager>();
        manager1.CanHandle(state).Returns(false);

        var manager2 = Mock.Of<IUpgradeManager>();
        manager2.CanHandle(state).Returns(true);
        manager2.ProcessUpgradeAsync(state, Any<CancellationToken>()).Returns(UpgradeActionResult.Searched);

        var migrationState = Mock.Of<IMigrationState>();
        migrationState.IsDone.Returns(true);

        var service = new UpgradeService([manager1.Object, manager2.Object], dbContext, logger, timeProvider, migrationState.Object);

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
        var timeProvider = TimeProvider.System;
        var logger = NullLogger<UpgradeService>.Instance;

        var state = new SonarrUpgradeState
        {
            ItemId = 2,
            SearchState = SearchState.Pending,
            Metadata = new SonarrMetadata(SonarrItemType.Series),
        };
        dbContext.UpgradeStates.Add(state);
        await dbContext.SaveChangesAsync();

        var manager = Mock.Of<IUpgradeManager>();
        manager.CanHandle(state).Returns(true);
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

        var state = new SonarrUpgradeState
        {
            ItemId = 3,
            Metadata = new SonarrMetadata(SonarrItemType.Episode),
            SearchState = SearchState.Pending,
        };
        dbContext.UpgradeStates.Add(state);
        await dbContext.SaveChangesAsync();

        var manager = Mock.Of<IUpgradeManager>();
        manager.CanHandle(state).Returns(true);
        manager.ProcessUpgradeAsync(state, Any<CancellationToken>()).Throws(new Exception("API failure"));

        var migrationState = Mock.Of<IMigrationState>();
        migrationState.IsDone.Returns(true);

        var service = new UpgradeService([manager.Object], dbContext, logger, timeProvider, migrationState.Object);

        await service.ProcessItemUpgradeAsync(state);

        var updatedState = await dbContext.UpgradeStates.FirstAsync();
        await Assert.That(updatedState.SearchState).IsEqualTo(SearchState.Pending);
    }
}
