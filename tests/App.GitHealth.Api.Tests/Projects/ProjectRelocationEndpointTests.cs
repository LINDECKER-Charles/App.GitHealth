using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using App.GitHealth.Api.Tests.Hosting;
using App.GitHealth.Api.Tests.Persistence;
using App.GitHealth.Core.Analysis;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace App.GitHealth.Api.Tests.Projects;

public sealed class ProjectRelocationEndpointTests
{
    [Fact]
    public async Task RelocationPreservesHistoryAndAllowsNewAnalysis()
    {
        using var repository = GitTestRepository.Create(aheadBranchCount: 1);
        using var factory = CreateFactory(repository.RootPath);
        using var client = factory.CreateClient();
        var projectId = await ApiTestWorkflow.CreateProjectAsync(
            client,
            repository.RepositoryPath);
        var firstAnalysisId = await ApiTestWorkflow.AnalyzeAsync(client, projectId);

        var move = await MoveAndMarkUnavailableAsync(client, projectId, repository);
        var relocated = await RelocateAsync(client, projectId, move.RepositoryPath);
        var expected = new RelocationExpectation(
            projectId,
            move.RepositoryPath,
            firstAnalysisId);
        AssertRelocation(relocated, expected);

        var secondAnalysisId = await ApiTestWorkflow.AnalyzeAsync(client, projectId);
        await AssertHistoryAsync(
            client,
            projectId,
            [firstAnalysisId, move.FailedAnalysisId, secondAnalysisId]);
        await AssertLastSuccessfulAnalysisAsync(client, projectId, secondAnalysisId);
    }

    [Fact]
    public async Task RelocationRejectsRepositoryAlreadyAttachedToAnotherProject()
    {
        using var first = GitTestRepository.Create(aheadBranchCount: 0);
        using var second = GitTestRepository.Create(aheadBranchCount: 0);
        var repositoriesRoot = Path.GetDirectoryName(first.RootPath)!;
        using var factory = CreateFactory(repositoriesRoot);
        using var client = factory.CreateClient();
        var firstProjectId = await ApiTestWorkflow.CreateProjectAsync(
            client,
            first.RepositoryPath);
        await ApiTestWorkflow.CreateProjectAsync(client, second.RepositoryPath);

        using var response = await client.PutAsJsonAsync(
            $"/api/projects/{firstProjectId}/repository",
            new { repositoryPath = second.RepositoryPath });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal(
            "project.already_exists",
            await ApiTestWorkflow.ReadProblemCodeAsync(response));
        await AssertRepositoryPathAsync(client, firstProjectId, first.RepositoryPath);
    }

    [Fact]
    public async Task RelocationRejectsRepositoryWithoutConfiguredReference()
    {
        using var source = GitTestRepository.Create(aheadBranchCount: 1);
        using var target = GitTestRepository.Create(aheadBranchCount: 0);
        var repositoriesRoot = Path.GetDirectoryName(source.RootPath)!;
        using var factory = CreateFactory(repositoriesRoot);
        using var client = factory.CreateClient();
        var projectId = await ApiTestWorkflow.CreateProjectAsync(client, source.RepositoryPath);
        await ConfigureReferenceAsync(client, projectId, "refs/heads/feature/near-00");

        using var response = await client.PutAsJsonAsync(
            $"/api/projects/{projectId}/repository",
            new { repositoryPath = target.RepositoryPath });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(
            "repository.invalid_reference",
            await ApiTestWorkflow.ReadProblemCodeAsync(response));
        await AssertRepositoryPathAsync(client, projectId, source.RepositoryPath);
    }

    [Fact]
    public async Task RelocationRejectsRepositoryWithoutLastKnownCommit()
    {
        using var source = GitTestRepository.Create(aheadBranchCount: 0);
        using var target = GitTestRepository.Create(aheadBranchCount: 0);
        source.AddMainCommit();
        var repositoriesRoot = Path.GetDirectoryName(source.RootPath)!;
        using var factory = CreateFactory(repositoriesRoot);
        using var client = factory.CreateClient();
        var projectId = await ApiTestWorkflow.CreateProjectAsync(
            client,
            source.RepositoryPath);
        await ApiTestWorkflow.AnalyzeAsync(client, projectId);

        using var response = await client.PutAsJsonAsync(
            $"/api/projects/{projectId}/repository",
            new { repositoryPath = target.RepositoryPath });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal(
            "repository.identity_mismatch",
            await ApiTestWorkflow.ReadProblemCodeAsync(response));
        await AssertRepositoryPathAsync(client, projectId, source.RepositoryPath);
    }

    [Fact]
    public async Task RelocationRejectsActiveAnalysisWithoutChangingPath()
    {
        using var source = GitTestRepository.Create(aheadBranchCount: 0);
        using var target = GitTestRepository.Create(aheadBranchCount: 0);
        var scanner = new ControlledRepositoryScanner(
            PersistenceTestData.CreateScan(DateTimeOffset.UtcNow));
        var repositoriesRoot = Path.GetDirectoryName(source.RootPath)!;
        using var factory = CreateFactory(repositoriesRoot, scanner);
        using var client = factory.CreateClient();
        var projectId = await ApiTestWorkflow.CreateProjectAsync(
            client,
            source.RepositoryPath);

        var launch = await ApiTestWorkflow.LaunchAsync(client, projectId);
        using var launchResponse = launch.Response;
        await scanner.Started.WaitAsync(TimeSpan.FromSeconds(10));
        using var response = await client.PutAsJsonAsync(
            $"/api/projects/{projectId}/repository",
            new { repositoryPath = target.RepositoryPath });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("project.busy", await ApiTestWorkflow.ReadProblemCodeAsync(response));
        await AssertRepositoryPathAsync(client, projectId, source.RepositoryPath);
        scanner.Release();
        await ApiTestWorkflow.WaitForStatusAsync(client, launch.Id, "Completed");
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
            TestServices = services => ReplaceScanner(services, scanner),
        };

    private static void ReplaceScanner(
        IServiceCollection services,
        IRepositoryScanner scanner)
    {
        services.RemoveAll<IRepositoryScanner>();
        services.AddSingleton(scanner);
    }

    private static async Task ConfigureReferenceAsync(
        HttpClient client,
        Guid projectId,
        string referenceName)
    {
        var request = new
        {
            referenceName,
            branchNamespace = "refs/heads/feature/*",
            activeUntilDays = 30,
            inactiveAfterDays = 90,
            excludedPatterns = Array.Empty<string>(),
            protectedPatterns = Array.Empty<string>(),
        };
        using var response = await client.PutAsJsonAsync(
            $"/api/projects/{projectId}/settings",
            request);
        response.EnsureSuccessStatusCode();
    }

    private static async Task<RepositoryMove> MoveAndMarkUnavailableAsync(
        HttpClient client,
        Guid projectId,
        GitTestRepository repository)
    {
        var relocatedPath = Path.Combine(repository.RootPath, "relocated-repository");
        Directory.Move(repository.RepositoryPath, relocatedPath);
        var launch = await ApiTestWorkflow.LaunchAsync(client, projectId);
        using var response = launch.Response;
        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        await ApiTestWorkflow.WaitForStatusAsync(client, launch.Id, "Failed");
        var project = await ReadProjectAsync(client, projectId);
        Assert.False(project.GetProperty("isRepositoryAccessible").GetBoolean());
        return new RepositoryMove(relocatedPath, launch.Id);
    }

    private static async Task<JsonElement> RelocateAsync(
        HttpClient client,
        Guid projectId,
        string repositoryPath)
    {
        using var response = await client.PutAsJsonAsync(
            $"/api/projects/{projectId}/repository",
            new { repositoryPath });
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }

    private static void AssertRelocation(
        JsonElement project,
        RelocationExpectation expected)
    {
        Assert.Equal(expected.ProjectId, project.GetProperty("id").GetGuid());
        Assert.Equal(
            Path.GetFullPath(expected.RepositoryPath),
            project.GetProperty("repositoryPath").GetString());
        Assert.True(project.GetProperty("isRepositoryAccessible").GetBoolean());
        Assert.Equal(
            expected.LastSuccessfulAnalysisId,
            project.GetProperty("lastSuccessfulAnalysisId").GetGuid());
    }

    private static async Task AssertHistoryAsync(
        HttpClient client,
        Guid projectId,
        IReadOnlyCollection<Guid> expectedAnalysisIds)
    {
        var history = await client.GetFromJsonAsync<JsonElement>(
            $"/api/projects/{projectId}/analyses?pageSize=20");
        Assert.Equal(expectedAnalysisIds.Count, history.GetProperty("totalCount").GetInt32());
        var actualIds = history.GetProperty("items")
            .EnumerateArray()
            .Select(item => item.GetProperty("analysisId").GetGuid())
            .ToArray();
        Assert.Equal(expectedAnalysisIds.Order(), actualIds.Order());
    }

    private static async Task AssertLastSuccessfulAnalysisAsync(
        HttpClient client,
        Guid projectId,
        Guid expectedAnalysisId)
    {
        var project = await ReadProjectAsync(client, projectId);
        Assert.Equal(
            expectedAnalysisId,
            project.GetProperty("lastSuccessfulAnalysisId").GetGuid());
    }

    private static async Task AssertRepositoryPathAsync(
        HttpClient client,
        Guid projectId,
        string expectedPath)
    {
        var project = await ReadProjectAsync(client, projectId);
        Assert.Equal(
            Path.GetFullPath(expectedPath),
            project.GetProperty("repositoryPath").GetString());
    }

    private static async Task<JsonElement> ReadProjectAsync(
        HttpClient client,
        Guid projectId) =>
        await client.GetFromJsonAsync<JsonElement>($"/api/projects/{projectId}");

    private sealed record RepositoryMove(string RepositoryPath, Guid FailedAnalysisId);

    private sealed record RelocationExpectation(
        Guid ProjectId,
        string RepositoryPath,
        Guid LastSuccessfulAnalysisId);
}
