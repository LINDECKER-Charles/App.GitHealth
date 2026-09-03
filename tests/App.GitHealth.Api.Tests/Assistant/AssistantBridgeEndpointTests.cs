using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using App.GitHealth.Api.Tests.Hosting;

namespace App.GitHealth.Api.Tests.Assistant;

public sealed class AssistantBridgeEndpointTests
{
    private const string UnopenedToken = "5f1c0e9a2b7d4c6e8f0a1b2c3d4e5f60";
    private const string JsonRpcError = "error";

    /// <summary>
    /// The bridge sits outside <c>/api</c>, so the session cookie the browser is guarded by
    /// protects nothing here: the token of a live run is the entire authorisation. Without
    /// that check, one capture would be readable by anything that reaches the loopback port.
    /// </summary>
    [Fact]
    public async Task ATokenThatWasNeverOpenedReadsNoCapture()
    {
        using var factory = new ApiApplicationFactory();
        using var client = factory.CreateRawClient();

        using var response = await client.PostAsJsonAsync(
            $"/agent-bridge/{UnopenedToken}",
            new { jsonrpc = "2.0", id = 1, method = "tools/list" });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        var payload = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(payload.TryGetProperty(JsonRpcError, out _));
    }
}
