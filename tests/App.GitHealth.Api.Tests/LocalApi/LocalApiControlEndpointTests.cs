using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using App.GitHealth.Api.Features.LocalApi;
using App.GitHealth.Api.Tests.Hosting;

namespace App.GitHealth.Api.Tests.LocalApi;

public sealed class LocalApiControlEndpointTests
{
    [Fact]
    public async Task AccessStartsClosedAndWithoutToken()
    {
        using var factory = new ApiApplicationFactory();
        using var control = factory.CreateClient();

        var state = await LocalApiWorkspace.DescribeAsync(control);

        Assert.False(state.GetProperty("isEnabled").GetBoolean());
        Assert.False(state.GetProperty("hasToken").GetBoolean());
        Assert.Equal("Closed", state.GetProperty("status").GetString());
        Assert.Equal(LocalApiSettings.DefaultPort, state.GetProperty("port").GetInt32());
        Assert.Equal(JsonValueKind.Null, state.GetProperty("address").ValueKind);
    }

    [Fact]
    public async Task OpeningWithoutATokenIsRefused()
    {
        using var factory = new ApiApplicationFactory();
        using var control = factory.CreateClient();

        using var response = await control.PutAsJsonAsync(
            LocalApiWorkspace.ControlPath,
            new { isEnabled = true, port = LocalApiWorkspace.BorrowPort() });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("local_api.token_required", await ApiTestWorkflow.ReadProblemCodeAsync(response));
    }

    [Theory]
    [InlineData(80)]
    [InlineData(0)]
    [InlineData(70000)]
    public async Task PortOutsideTheAllowedRangeIsRefused(int port)
    {
        using var factory = new ApiApplicationFactory();
        using var control = factory.CreateClient();

        using var response = await control.PutAsJsonAsync(
            LocalApiWorkspace.ControlPath,
            new { isEnabled = false, port });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("local_api.invalid_port", await ApiTestWorkflow.ReadProblemCodeAsync(response));
    }

    [Fact]
    public async Task TokenIsHandedOverOnceAndOnlyItsPrefixIsKept()
    {
        using var factory = new ApiApplicationFactory();
        using var control = factory.CreateClient();

        var token = await LocalApiWorkspace.IssueTokenAsync(control);
        var state = await LocalApiWorkspace.DescribeAsync(control);

        Assert.False(string.IsNullOrWhiteSpace(token));
        Assert.True(state.GetProperty("hasToken").GetBoolean());
        Assert.Equal(token[..LocalApiToken.PrefixLength], state.GetProperty("tokenPrefix").GetString());
        Assert.False(state.TryGetProperty("token", out _));
        Assert.DoesNotContain(token, state.GetRawText(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task OpeningTakesTheRequestedPortAndClosingGivesItBack()
    {
        using var factory = new ApiApplicationFactory();
        using var control = factory.CreateClient();
        await LocalApiWorkspace.IssueTokenAsync(control);
        var port = LocalApiWorkspace.BorrowPort();

        var opened = await LocalApiWorkspace.ApplyAsync(control, isEnabled: true, port);
        var closed = await LocalApiWorkspace.ApplyAsync(control, isEnabled: false, port);

        Assert.Equal("Listening", opened.GetProperty("status").GetString());
        Assert.Equal($"http://127.0.0.1:{port}/", opened.GetProperty("address").GetString());
        Assert.Equal("Closed", closed.GetProperty("status").GetString());
        Assert.Equal(JsonValueKind.Null, closed.GetProperty("address").ValueKind);
    }

    /// <summary>
    /// A port already taken must not bring the application down with it: the setting is kept,
    /// the access reports why it failed, and the interface stays usable.
    /// </summary>
    [Fact]
    public async Task PortAlreadyTakenIsReportedWithoutBringingTheApplicationDown()
    {
        using var factory = new ApiApplicationFactory();
        using var control = factory.CreateClient();
        await LocalApiWorkspace.IssueTokenAsync(control);
        var occupied = new System.Net.Sockets.TcpListener(IPAddress.Loopback, 0);
        occupied.Start();
        var port = ((IPEndPoint)occupied.LocalEndpoint).Port;

        try
        {
            var state = await LocalApiWorkspace.ApplyAsync(control, isEnabled: true, port);

            Assert.Equal("Failed", state.GetProperty("status").GetString());
            Assert.True(state.GetProperty("isEnabled").GetBoolean());
            Assert.False(string.IsNullOrWhiteSpace(state.GetProperty("failureMessage").GetString()));
        }
        finally
        {
            occupied.Stop();
        }

        using var stillAlive = await control.GetAsync("/api/runtime");
        Assert.Equal(HttpStatusCode.OK, stillAlive.StatusCode);
    }

    /// <summary>The access is the user's decision, so it survives a restart as they left it.</summary>
    [Fact]
    public async Task SettingsSurviveARestart()
    {
        using var directory = new ScopedDataDirectory();
        var port = LocalApiWorkspace.BorrowPort();
        string prefix;
        using (var first = directory.CreateFactory())
        {
            using var control = first.CreateClient();
            var token = await LocalApiWorkspace.IssueTokenAsync(control);
            prefix = token[..LocalApiToken.PrefixLength];
            await LocalApiWorkspace.ApplyAsync(control, isEnabled: true, port);

            // Stopping is what releases the port; disposing alone would leave the second run
            // fighting the first one for it.
            await first.StopHostAsync(CancellationToken.None);
        }

        using var second = directory.CreateFactory();
        using var restarted = second.CreateClient();
        var state = await LocalApiWorkspace.DescribeAsync(restarted);

        Assert.True(state.GetProperty("isEnabled").GetBoolean());
        Assert.Equal(port, state.GetProperty("port").GetInt32());
        Assert.Equal(prefix, state.GetProperty("tokenPrefix").GetString());
        Assert.Equal("Listening", state.GetProperty("status").GetString());
    }
}
