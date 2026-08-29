using System.Net;
using Microsoft.AspNetCore.Hosting;

namespace App.GitHealth.Api.Tests.Hosting;

public sealed class SpaHostingTests(ApiApplicationFactory factory)
    : IClassFixture<ApiApplicationFactory>
{
    [Theory]
    [InlineData("/")]
    [InlineData("/projects/example")]
    public async Task GetClientRouteReturnsSpaEntryPoint(string path)
    {
        var webRoot = Path.Combine(AppContext.BaseDirectory, "StaticFiles");
        using var configuredFactory = factory.WithWebHostBuilder(
            builder => builder.UseWebRoot(webRoot));
        using var client = configuredFactory.CreateClient();

        using var response = await client.GetAsync(path);
        var content = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/html", response.Content.Headers.ContentType?.MediaType);
        Assert.Contains("GitHealth test shell", content);
    }
}
