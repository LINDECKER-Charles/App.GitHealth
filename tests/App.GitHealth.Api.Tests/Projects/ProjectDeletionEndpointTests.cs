using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using App.GitHealth.Api.Tests.Hosting;
using App.GitHealth.Api.Tests.Persistence;
using App.GitHealth.Core.Analysis;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace App.GitHealth.Api.Tests.Projects;

public sealed class ProjectDeletionEndpointTests
{
    private static readonly TimeSpan TestTimeout = TimeSpan.FromSeconds(10);

    [Fact]
    public async Task ProjectCanBeDeletedAndItsPathBecomesReusable()
    {
        using var repository = GitTestRepository.Create(aheadBranchCount: 1);
        using var factory = CreateFactory(repository.RootPath);
        using var client = factory.CreateClient();
        var projectId = await ApiTestWorkflow.CreateProjectAsync(
            client,
            repository.RepositoryPath);
        await ApiTestWorkflow.AnalyzeAsync(client, projectId);
        await AssertDiscoveryTracksAsync(client, repository.RootPath, projectId);

        using var deletion = await client.DeleteAsync($"/api/projects/{projectId}");

        Assert.Equal(HttpStatusCode.NoContent, deletion.StatusCode);
        await AssertProjectIsGoneAsync(client, projectId);
        await AssertDiscoveryTracksAsync(client, repository.RootPath, expected: null);
    }

    [Fact]
    public async Task DeletingAnUnknownProjectIsNotFound()
    {
        using var repository = GitTestRepository.Create(aheadBranchCount: 0);
        using var factory = CreateFactory(repository.RootPath);
        using var client = factory.CreateClient();

        using var response = await client.DeleteAsync($"/api/projects/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("project.not_found", await ApiTestWorkflow.ReadProblemCodeAsync(response));
    }

    [Fact]
    public async Task DeletingABusyProjectIsRejected()
    {
        using var repository = GitTestRepository.Create(aheadBranchCount: 0);
        var scanner = new ControlledRepositoryScanner(
            PersistenceTestData.CreateScan(DateTimeOffset.UtcNow));
        using var factory = CreateFactory(repository.RootPath, scanner);
        using var client = factory.CreateClient();
        var projectId = await ApiTestWorkflow.CreateProjectAsync(
            client,
            repository.RepositoryPath);

        var launch = await ApiTestWorkflow.LaunchAsync(client, projectId);
        using var launchResponse = launch.Response;
        await scanner.Started.WaitAsync(TestTimeout);
        using var response = await client.DeleteAsync($"/api/projects/{projectId}");

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("project.busy", await ApiTestWorkflow.ReadProblemCodeAsync(response));
        scanner.Release();
        await ApiTestWorkflow.WaitForStatusAsync(client, launch.Id, "Completed");
        await AssertProjectExistsAsync(client, projectId);
    }

    private static ApiApplicationFactory CreateFactory(string repositoriesRoot) => new()
    {
        RepositoriesRoot = repositoriesRoot,
    };

    private static ApiApplicationFactory CreateFactory(
        string repositoriesRoot,
        IRepositoryScanner scanner) => new()
        {
            RepositoriesRoot = repositoriesRoot,
            TestServices = services =>
            {
                services.RemoveAll<IRepositoryScanner>();
                services.AddSingleton(scanner);
            },
        };

    private static async Task AssertProjectIsGoneAsync(HttpClient client, Guid projectId)
    {
        using var response = await client.GetAsync($"/api/projects/{projectId}");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("project.not_found", await ApiTestWorkflow.ReadProblemCodeAsync(response));

        var projects = await client.GetFromJsonAsync<JsonElement[]>("/api/projects");
        Assert.NotNull(projects);
        Assert.DoesNotContain(projects, item => item.GetProperty("id").GetGuid() == projectId);
    }

    private static async Task AssertProjectExistsAsync(HttpClient client, Guid projectId)
    {
        using var response = await client.GetAsync($"/api/projects/{projectId}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    /// <summary>
    /// Discovery is what decides whether a repository can be added again, so it is the
    /// observable proof that deleting a project truly releases its path.
    /// </summary>
    private static async Task AssertDiscoveryTracksAsync(
        HttpClient client,
        string rootPath,
        Guid? expected)
    {
        using var response = await client.PostAsJsonAsync(
            "/api/repositories/discover",
            new { path = rootPath });
        response.EnsureSuccessStatusCode();
        var payload = await response.Content.ReadFromJsonAsync<JsonElement>();
        var found = Assert.Single(payload.GetProperty("repositories").EnumerateArray());
        var tracked = found.GetProperty("trackedProjectId");
        Assert.Equal(expected, tracked.ValueKind == JsonValueKind.Null
            ? null
            : tracked.GetGuid());
    }
}
