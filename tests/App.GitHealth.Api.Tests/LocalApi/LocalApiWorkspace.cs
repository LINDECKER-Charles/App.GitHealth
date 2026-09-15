using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Net.Sockets;
using System.Text.Json;
using App.GitHealth.Api.Tests.Hosting;

namespace App.GitHealth.Api.Tests.LocalApi;

/// <summary>
/// Drives the access the way the settings screen does — issue a token, then open a port — and
/// hands back a client aimed at the port that was actually opened. The listener is real
/// Kestrel on a real socket, so these tests exercise the binding rather than a test double.
/// </summary>
internal static class LocalApiWorkspace
{
    public const string ControlPath = "/api/local-api";

    /// <summary>
    /// Borrows a port from the operating system and gives it straight back. Two tests running
    /// side by side therefore never ask for the same one.
    /// </summary>
    public static int BorrowPort()
    {
        var probe = new TcpListener(IPAddress.Loopback, 0);
        probe.Start();
        var port = ((IPEndPoint)probe.LocalEndpoint).Port;
        probe.Stop();
        return port;
    }

    public static async Task<string> IssueTokenAsync(HttpClient control)
    {
        using var response = await control.PostAsync($"{ControlPath}/token", content: null);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var payload = await response.Content.ReadFromJsonAsync<JsonElement>();
        return payload.GetProperty("token").GetString()!;
    }

    public static async Task<JsonElement> ApplyAsync(
        HttpClient control,
        bool isEnabled,
        int port)
    {
        using var response = await control.PutAsJsonAsync(
            ControlPath,
            new { isEnabled, port });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }

    public static async Task<JsonElement> DescribeAsync(HttpClient control)
    {
        using var response = await control.GetAsync(ControlPath);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }

    /// <summary>Issues a token, opens the access and returns both the token and its client.</summary>
    public static async Task<LocalApiAccess> OpenAsync(HttpClient control)
    {
        var token = await IssueTokenAsync(control);
        var port = BorrowPort();
        var state = await ApplyAsync(control, isEnabled: true, port);
        Assert.Equal("Listening", state.GetProperty("status").GetString());
        return new LocalApiAccess(token, port, CreateClient(port, token));
    }

    public static HttpClient CreateClient(int port, string? token)
    {
        var client = new HttpClient
        {
            BaseAddress = new Uri($"http://127.0.0.1:{port}"),
            Timeout = TimeSpan.FromSeconds(30),
        };
        if (token is not null)
        {
            client.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", token);
        }

        return client;
    }

    public static async Task<Guid> CreateProjectAsync(
        HttpClient control,
        GitTestRepository repository) =>
        await ApiTestWorkflow.CreateProjectAsync(control, repository.RepositoryPath);
}

internal sealed record LocalApiAccess(string Token, int Port, HttpClient Client) : IDisposable
{
    public void Dispose() => Client.Dispose();
}
