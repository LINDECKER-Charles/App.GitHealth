using System.Net;
using System.Text.Json;
using App.GitHealth.Api.Tests.Hosting;

namespace App.GitHealth.Api.Tests.OpenApi;

public sealed class OpenApiEndpointTests(ApiApplicationFactory factory)
    : IClassFixture<ApiApplicationFactory>
{
    [Fact]
    public async Task GetOpenApiReturnsDocument()
    {
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/openapi/v1.json");
        await using var body = await response.Content.ReadAsStreamAsync();
        using var document = await JsonDocument.ParseAsync(body);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.StartsWith("3.", document.RootElement.GetProperty("openapi").GetString());
        Assert.True(document.RootElement.TryGetProperty("info", out _));
    }
}
