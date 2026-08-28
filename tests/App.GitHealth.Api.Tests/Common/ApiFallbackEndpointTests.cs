using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using App.GitHealth.Api.Tests.Hosting;

namespace App.GitHealth.Api.Tests.Common;

public sealed class ApiFallbackEndpointTests
{
    [Theory]
    [InlineData("/api/unknown")]
    [InlineData("/api/projects/not-a-guid")]
    public async Task UnknownApiRoutesReturnStableProblemDetails(string path)
    {
        using var factory = new ApiApplicationFactory();
        using var client = factory.CreateClient();

        using var response = await client.GetAsync(path);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("endpoint.not_found", problem.GetProperty("code").GetString());
        Assert.False(string.IsNullOrWhiteSpace(problem.GetProperty("traceId").GetString()));
    }
}
