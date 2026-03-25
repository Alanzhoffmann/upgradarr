using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TUnit.AspNetCore;
using Upgradarr.Contracts;
using Upgradarr.Data.Interfaces;

namespace Upgradarr.Api.Tests;

public class MyTestFactory : TestWebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        var dbId = Guid.NewGuid().ToString("N");
        builder.ConfigureAppConfiguration(
            (context, config) =>
            {
                config.AddInMemoryCollection(
                    new Dictionary<string, string?> { ["Data:ConnectionString"] = $"Data Source=file:{dbId}?mode=memory&cache=shared" }
                );
            }
        );

        base.ConfigureWebHost(builder);
    }
}

public class EndpointTests : WebApplicationTest<MyTestFactory, Program>
{
    [Before(Test)]
    public async Task WaitForMigrations()
    {
        var migrationState = Factory.Server.Services.GetRequiredService<IMigrationState>();
        while (!migrationState.IsDone)
        {
            await Task.Delay(100);
        }
    }

    [Test]
    public async Task GetUpgrades_ReturnsSuccess()
    {
        var client = Factory.CreateClient();
        var response = await client.GetAsync("/upgrade");
        response.EnsureSuccessStatusCode();

        var upgrades = await response.Content.ReadFromJsonAsync<List<UpgradeStateDto>>();
        await Assert.That(upgrades).IsNotNull();
    }

    [Test]
    public async Task GetCleanups_ReturnsSuccess()
    {
        var client = Factory.CreateClient();
        var response = await client.GetAsync("/cleanup");
        response.EnsureSuccessStatusCode();

        var cleanups = await response.Content.ReadFromJsonAsync<List<QueueRecordDto>>();
        await Assert.That(cleanups).IsNotNull();
    }
}
