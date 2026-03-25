using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using TUnit.AspNetCore;
using Upgradarr.Contracts;

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
