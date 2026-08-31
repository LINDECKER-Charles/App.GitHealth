using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using App.GitHealth.Api.Tests.Hosting;

namespace App.GitHealth.Api.Tests.Discovery;

public sealed class RepositoryDiscoveryEndpointTests
{
    private const string DiscoverUrl = "/api/repositories/discover";

    [Fact]
    public async Task DiscoveryListsRepositoriesFoundUnderTheRequestedFolder()
    {
        using var workspace = DiscoveryWorkspace.Create();
        workspace.AddRepository("alpha");
        workspace.AddRepository(Path.Combine("group", "beta"));
        workspace.AddDirectory("documents");
        using var factory = new ApiApplicationFactory();
        using var client = factory.CreateClient();

        var payload = await DiscoverAsync(client, workspace.RootPath);

        Assert.Equal(["alpha", "beta"], Names(payload));
        Assert.False(payload.GetProperty("isTruncated").GetBoolean());
        var first = payload.GetProperty("repositories")[0];
        Assert.EndsWith("alpha", CanonicalPath(first), StringComparison.Ordinal);
        Assert.Equal("refs/heads/main", first.GetProperty("suggestedReference").GetString());
        Assert.False(first.GetProperty("isBare").GetBoolean());
        Assert.Equal(JsonValueKind.Null, first.GetProperty("trackedProjectId").ValueKind);
        var second = payload.GetProperty("repositories")[1];
        Assert.EndsWith(
            Path.Combine("group", "beta"),
            CanonicalPath(second),
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task DiscoveryStopsInsideAFoundRepository()
    {
        using var workspace = DiscoveryWorkspace.Create();
        workspace.AddRepository("alpha");
        workspace.AddRepository(Path.Combine("alpha", "embedded"));
        using var factory = new ApiApplicationFactory();
        using var client = factory.CreateClient();

        var payload = await DiscoverAsync(client, workspace.RootPath);

        Assert.Equal(["alpha"], Names(payload));
    }

    [Fact]
    public async Task DiscoveryFlagsRepositoriesAlreadyRegistered()
    {
        using var workspace = DiscoveryWorkspace.Create();
        var alpha = workspace.AddRepository("alpha");
        workspace.AddRepository("beta");
        using var factory = new ApiApplicationFactory();
        using var client = factory.CreateClient();
        var projectId = await ApiTestWorkflow.CreateProjectAsync(client, alpha);

        var payload = await DiscoverAsync(client, workspace.RootPath);

        Assert.Equal(["alpha", "beta"], Names(payload));
        var tracked = payload.GetProperty("repositories")[0];
        Assert.Equal(projectId, tracked.GetProperty("trackedProjectId").GetGuid());
        var untracked = payload.GetProperty("repositories")[1];
        Assert.Equal(JsonValueKind.Null, untracked.GetProperty("trackedProjectId").ValueKind);
    }

    [Fact]
    public async Task DiscoveryHonoursTheRequestedDepth()
    {
        using var workspace = DiscoveryWorkspace.Create();
        workspace.AddRepository(Path.Combine("group", "nested", "gamma"));
        using var factory = new ApiApplicationFactory();
        using var client = factory.CreateClient();

        var shallow = await DiscoverAsync(client, workspace.RootPath, depth: 2);
        var deep = await DiscoverAsync(client, workspace.RootPath, depth: 3);

        Assert.Empty(Names(shallow));
        Assert.Equal(["gamma"], Names(deep));
    }

    [Fact]
    public async Task DiscoveryRejectsFolderOutsideConfiguredRootInDockerMode()
    {
        using var root = DiscoveryWorkspace.Create();
        using var outside = DiscoveryWorkspace.Create();
        using var factory = new ApiApplicationFactory { RepositoriesRoot = root.RootPath };
        using var client = factory.CreateClient();

        using var response = await PostAsync(client, outside.RootPath, depth: null);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal(
            "runtime.directory_not_allowed",
            await ApiTestWorkflow.ReadProblemCodeAsync(response));
    }

    [Fact]
    public async Task DiscoveryReportsMissingFolderWithoutTechnicalDetails()
    {
        using var workspace = DiscoveryWorkspace.Create();
        using var factory = new ApiApplicationFactory();
        using var client = factory.CreateClient();

        using var response = await PostAsync(
            client,
            workspace.Resolve("missing"),
            depth: null);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal(
            "runtime.directory_not_found",
            await ApiTestWorkflow.ReadProblemCodeAsync(response));
    }

    private static async Task<JsonElement> DiscoverAsync(
        HttpClient client,
        string path,
        int? depth = null)
    {
        using var response = await PostAsync(client, path, depth);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }

    private static Task<HttpResponseMessage> PostAsync(
        HttpClient client,
        string path,
        int? depth) =>
        client.PostAsJsonAsync(DiscoverUrl, new { path, depth });

    private static string[] Names(JsonElement payload) => payload
        .GetProperty("repositories")
        .EnumerateArray()
        .Select(repository => repository.GetProperty("suggestedName").GetString()!)
        .ToArray();

    /// <summary>
    /// The returned path is the one Git canonicalises: on macOS it goes through the
    /// <c>/var</c> link, so only the end of the path is comparable to the test folder.
    /// </summary>
    private static string CanonicalPath(JsonElement repository) =>
        repository.GetProperty("canonicalPath").GetString()!;
}
