using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using TUnit.Playwright;

namespace Upgradarr.Web.Tests;

public class WebE2ETests : PageTest
{
    private WebApplicationFactory<Program> _factory = null!;
    private HttpClient _client = null!;

    [Before(Test)]
    public void SetupServer()
    {
        _factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseUrls("http://127.0.0.1:0");
        });
        _client = _factory.CreateClient();
    }

    [Test]
    public async Task Navigation_ToCleanups_Works()
    {
        var serverAddress = _factory.Server.BaseAddress;

        await Page.GotoAsync(serverAddress.ToString());

        await Assert.That(await Page.TitleAsync()).Contains("Upgradarr");

        await Page.ClickAsync("text=Cleanups");

        await Assert.That(await Page.Locator("h1").InnerTextAsync()).IsEqualTo("Cleanups");
    }

    [After(Test)]
    public async Task CleanupServer()
    {
        _client?.Dispose();
        if (_factory != null)
        {
            await _factory.DisposeAsync();
        }
    }
}
