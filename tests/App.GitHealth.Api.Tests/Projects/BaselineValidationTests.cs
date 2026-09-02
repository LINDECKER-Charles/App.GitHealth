using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using App.GitHealth.Api.Tests.Hosting;
using App.GitHealth.Api.Tests.Persistence;
using App.GitHealth.Core.Analysis;
using App.GitHealth.Core.Projects;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace App.GitHealth.Api.Tests.Projects;

public sealed class BaselineValidationTests
{
    private const string Primary = "refs/heads/main";
    private static readonly TimeSpan TestTimeout = TimeSpan.FromSeconds(10);

    [Fact]
    public async Task AnUnknownReferenceIsRejected()
    {
        using var repository = GitTestRepository.Create(aheadBranchCount: 0);
        using var factory = CreateFactory(repository.RootPath);
        using var client = factory.CreateClient();
        var projectId = await ApiTestWorkflow.CreateProjectAsync(
            client,
            repository.RepositoryPath);

        await AssertRejectedAsync(
            client,
            projectId,
            new Rejection([Primary, "refs/heads/nowhere"], "repository.invalid_reference"));
        await AssertBaselinesAsync(client, projectId, Primary);
    }

    [Fact]
    public async Task AnEmptyBaselineListIsRejected()
    {
        using var repository = GitTestRepository.Create(aheadBranchCount: 0);
        using var factory = CreateFactory(repository.RootPath);
        using var client = factory.CreateClient();
        var projectId = await ApiTestWorkflow.CreateProjectAsync(
            client,
            repository.RepositoryPath);

        await AssertRejectedAsync(
            client,
            projectId,
            new Rejection([], "validation.invalid_request"));
        await AssertBaselinesAsync(client, projectId, Primary);
    }

    [Fact]
    public async Task ARepeatedBaselineIsRejected()
    {
        using var repository = GitTestRepository.Create(aheadBranchCount: 0);
        using var factory = CreateFactory(repository.RootPath);
        using var client = factory.CreateClient();
        var projectId = await ApiTestWorkflow.CreateProjectAsync(
            client,
            repository.RepositoryPath);

        await AssertRejectedAsync(
            client,
            projectId,
            new Rejection([Primary, Primary], "validation.invalid_request"));
        await AssertBaselinesAsync(client, projectId, Primary);
    }

    [Fact]
    public async Task MoreThanTheMaximumNumberOfBaselinesIsRejected()
    {
        using var repository = GitTestRepository.Create(aheadBranchCount: 0);
        var requested = CreateExtraBranches(repository);
        using var factory = CreateFactory(repository.RootPath);
        using var client = factory.CreateClient();
        var projectId = await ApiTestWorkflow.CreateProjectAsync(
            client,
            repository.RepositoryPath);

        Assert.Equal(ProjectSettings.MaximumBaselineCount + 1, requested.Length);
        await AssertRejectedAsync(
            client,
            projectId,
            new Rejection(requested, "validation.invalid_request"));
        await AssertBaselinesAsync(client, projectId, Primary);
    }

    [Fact]
    public async Task ReplacingBaselinesDuringAnAnalysisIsRejected()
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
        await AssertRejectedAsync(
            client,
            projectId,
            new Rejection(["refs/heads/develop"], "project.busy", HttpStatusCode.Conflict));

        scanner.Release();
        await ApiTestWorkflow.WaitForStatusAsync(client, launch.Id, "Completed");
        await AssertBaselinesAsync(client, projectId, Primary);
    }

    /// <summary>Enough real references to overflow the limit by exactly one.</summary>
    private static string[] CreateExtraBranches(GitTestRepository repository)
    {
        var names = new List<string> { Primary };
        for (var index = 0; index < ProjectSettings.MaximumBaselineCount; index++)
        {
            var branchName = $"baseline-{index:D2}";
            repository.AddSynchronizedBranch(branchName);
            names.Add($"refs/heads/{branchName}");
        }

        return names.ToArray();
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

    private static async Task AssertRejectedAsync(
        HttpClient client,
        Guid projectId,
        Rejection rejection)
    {
        using var response = await client.PutAsJsonAsync(
            $"/api/projects/{projectId}/baselines/",
            new { referenceNames = rejection.ReferenceNames });
        Assert.Equal(rejection.Status, response.StatusCode);
        Assert.Equal(rejection.Code, await ApiTestWorkflow.ReadProblemCodeAsync(response));
    }

    private static async Task AssertBaselinesAsync(
        HttpClient client,
        Guid projectId,
        params string[] expected)
    {
        var project = await client.GetFromJsonAsync<JsonElement>(
            $"/api/projects/{projectId}");
        Assert.Equal(
            expected,
            project.GetProperty("referenceNames")
                .EnumerateArray()
                .Select(value => value.GetString()!)
                .ToArray());
    }

    private sealed record Rejection(
        string[] ReferenceNames,
        string Code,
        HttpStatusCode Status = HttpStatusCode.BadRequest);
}
