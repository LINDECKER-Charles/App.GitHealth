using System.Net;
using System.Text.Json;
using App.GitHealth.Api.Tests.Hosting;

namespace App.GitHealth.Api.Tests.Health;

public sealed class HealthEndpointTests(ApiApplicationFactory factory)
    : IClassFixture<ApiApplicationFactory>
{
    [Fact]
    public async Task GetHealthReturnsHealthyResponse()
    {
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/health");
        await using var content = await response.Content.ReadAsStreamAsync();
        using var document = await JsonDocument.ParseAsync(content);
        var root = document.RootElement;
        var git = root.GetProperty("checks").GetProperty("git");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Healthy", root.GetProperty("status").GetString());
        Assert.Equal("Healthy", git.GetProperty("status").GetString());
        Assert.StartsWith("git version", git.GetProperty("message").GetString());
    }
}
