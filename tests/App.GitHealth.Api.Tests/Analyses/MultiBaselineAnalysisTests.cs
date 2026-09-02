using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using App.GitHealth.Api.Tests.Hosting;
using App.GitHealth.Api.Tests.Persistence;
using App.GitHealth.Core.Analysis;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace App.GitHealth.Api.Tests.Analyses;

/// <summary>
/// A launch fans out into one run per declared baseline, and each baseline owns its own
/// queue slot and its own latest capture.
/// </summary>
public sealed class MultiBaselineAnalysisTests
{
    private const string GatedBaseline = "refs/heads/develop";
    private const string PrimaryBaseline = "refs/heads/main";
    private const string SecondaryBaseline = "refs/heads/release";
    private const string SecondaryBranch = "release";
    private const string UndeclaredBaseline = "refs/heads/feature/behind";
    private static readonly TimeSpan TestTimeout = TimeSpan.FromSeconds(10);

    [Fact]
    public async Task OneLaunchProducesOneRunPerDeclaredBaseline()
    {
        using var repository = CreateRepository();
        using var factory = CreateFactory(repository.RootPath);
        using var client = factory.CreateClient();
        var projectId = await CreateProjectAsync(client, repository.RepositoryPath);

        var launch = await LaunchAsync(client, projectId, baseline: null);
        var items = Items(launch);

        Assert.Equal(new[] { PrimaryBaseline, SecondaryBaseline }, items.Select(ReferenceName));
        Assert.Equal(AnalysisId(items[0]), launch.GetProperty("analysisId").GetGuid());
        Assert.False(launch.GetProperty("isDuplicate").GetBoolean());
        await CompleteAllAsync(client, items);
        await AssertFrozenReferencesAsync(client, projectId, items);
    }

    [Fact]
    public async Task LaunchingASingleBaselineRunsOnlyThatOne()
    {
        using var repository = CreateRepository();
        using var factory = CreateFactory(repository.RootPath);
        using var client = factory.CreateClient();
        var projectId = await CreateProjectAsync(client, repository.RepositoryPath);

        var launch = await LaunchAsync(client, projectId, SecondaryBaseline);
        var item = Assert.Single(Items(launch));

        Assert.Equal(SecondaryBaseline, ReferenceName(item));
        await CompleteAllAsync(client, [item]);
        var history = await ReadHistoryAsync(client, projectId);
        var recorded = Assert.Single(history.GetProperty("items").EnumerateArray());
        Assert.Equal(SecondaryBaseline, ReferenceName(recorded));
    }

    [Fact]
    public async Task LaunchingAnUndeclaredBaselineIsRejected()
    {
        using var repository = CreateRepository();
        using var factory = CreateFactory(repository.RootPath);
        using var client = factory.CreateClient();
        var projectId = await CreateProjectAsync(client, repository.RepositoryPath);

        using var response = await client.PostAsync(
            LaunchUri(projectId, UndeclaredBaseline),
            content: null);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(
            "repository.invalid_reference",
            await ApiTestWorkflow.ReadProblemCodeAsync(response));
    }

    /// <summary>
    /// Re-keying the queue by (project, baseline) is what lets a second baseline start while
    /// the first one is still measuring; only the very same baseline reports a duplicate.
    /// </summary>
    [Fact]
    public async Task RelaunchingTheSameBaselineWhileItRunsIsADuplicate()
    {
        var scanner = new ControlledRepositoryScanner(
            PersistenceTestData.CreateScan(DateTimeOffset.UtcNow, "refs/heads/feature/gated"));
        using var repository = GitTestRepository.Create(aheadBranchCount: 0);
        using var factory = CreateGatedFactory(repository.RootPath, scanner);
        using var client = factory.CreateClient();
        var projectId = await ApiTestWorkflow.CreateProjectAsync(
            client,
            repository.RepositoryPath,
            ApiTestWorkflow.MultiBaselineSettings(PrimaryBaseline, GatedBaseline));

        var running = Assert.Single(Items(await LaunchAsync(client, projectId, PrimaryBaseline)));
        await scanner.Started.WaitAsync(TestTimeout);
        var repeated = Assert.Single(Items(await LaunchAsync(client, projectId, PrimaryBaseline)));
        var other = Assert.Single(Items(await LaunchAsync(client, projectId, GatedBaseline)));

        Assert.True(repeated.GetProperty("isDuplicate").GetBoolean());
        Assert.Equal(AnalysisId(running), AnalysisId(repeated));
        Assert.False(other.GetProperty("isDuplicate").GetBoolean());
        Assert.NotEqual(AnalysisId(running), AnalysisId(other));
        scanner.Release();
        await CompleteAllAsync(client, [running, other]);
    }

    [Fact]
    public async Task EachBaselineKeepsItsOwnLatestCapture()
    {
        using var repository = CreateRepository();
        using var factory = CreateFactory(repository.RootPath);
        using var client = factory.CreateClient();
        var projectId = await CreateProjectAsync(client, repository.RepositoryPath);
        var runs = await ApiTestWorkflow.AnalyzeAllAsync(client, projectId);

        var primary = await ReadLatestAsync(client, projectId, PrimaryBaseline);
        var secondary = await ReadLatestAsync(client, projectId, SecondaryBaseline);
        var unscoped = await ReadLatestAsync(client, projectId, baseline: null);

        Assert.Equal(PrimaryBaseline, ReferenceName(primary));
        Assert.Equal(SecondaryBaseline, ReferenceName(secondary));
        Assert.NotEqual(AnalysisId(primary), AnalysisId(secondary));
        Assert.Equal(RunFor(runs, SecondaryBaseline), AnalysisId(secondary));
        Assert.Equal(RunFor(runs, PrimaryBaseline), AnalysisId(unscoped));
    }

    private static GitTestRepository CreateRepository()
    {
        var repository = GitTestRepository.Create(aheadBranchCount: 2);
        repository.AddSynchronizedBranch(SecondaryBranch);
        return repository;
    }

    private static ApiApplicationFactory CreateFactory(string repositoriesRoot) => new()
    {
        RepositoriesRoot = repositoriesRoot,
    };

    private static ApiApplicationFactory CreateGatedFactory(
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

    private static Task<Guid> CreateProjectAsync(HttpClient client, string repositoryPath) =>
        ApiTestWorkflow.CreateProjectAsync(
            client,
            repositoryPath,
            ApiTestWorkflow.MultiBaselineSettings(PrimaryBaseline, SecondaryBaseline));

    private static async Task<JsonElement> LaunchAsync(
        HttpClient client,
        Guid projectId,
        string? baseline)
    {
        using var response = await client.PostAsync(
            LaunchUri(projectId, baseline),
            content: null);
        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }

    private static async Task CompleteAllAsync(
        HttpClient client,
        IEnumerable<JsonElement> launched)
    {
        foreach (var item in launched)
        {
            await ApiTestWorkflow.WaitForStatusAsync(client, AnalysisId(item), "Completed");
        }
    }

    /// <summary>Each run freezes the baseline it measured, whoever finished first.</summary>
    private static async Task AssertFrozenReferencesAsync(
        HttpClient client,
        Guid projectId,
        IEnumerable<JsonElement> launched)
    {
        var history = await ReadHistoryAsync(client, projectId);
        var recorded = history.GetProperty("items")
            .EnumerateArray()
            .ToDictionary(AnalysisId, ReferenceName);
        foreach (var item in launched)
        {
            Assert.Equal(ReferenceName(item), recorded[AnalysisId(item)]);
        }
    }

    private static async Task<JsonElement> ReadHistoryAsync(HttpClient client, Guid projectId) =>
        await client.GetFromJsonAsync<JsonElement>(
            $"/api/projects/{projectId}/analyses?pageSize=100");

    private static async Task<JsonElement> ReadLatestAsync(
        HttpClient client,
        Guid projectId,
        string? baseline)
    {
        var uri = $"/api/projects/{projectId}/analyses/latest/branches";
        if (baseline is not null)
        {
            uri += $"?baseline={Uri.EscapeDataString(baseline)}";
        }

        return await client.GetFromJsonAsync<JsonElement>(uri);
    }

    private static string LaunchUri(Guid projectId, string? baseline)
    {
        var uri = $"/api/projects/{projectId}/analyses";
        return baseline is null ? uri : $"{uri}?baseline={Uri.EscapeDataString(baseline)}";
    }

    private static Guid RunFor(
        IEnumerable<(Guid Id, string ReferenceName)> runs,
        string baseline) => runs.Single(run => run.ReferenceName == baseline).Id;

    private static JsonElement[] Items(JsonElement launch) =>
        launch.GetProperty("analyses").EnumerateArray().ToArray();

    private static Guid AnalysisId(JsonElement item) =>
        item.GetProperty("analysisId").GetGuid();

    private static string ReferenceName(JsonElement item) =>
        item.GetProperty("referenceName").GetString()!;
}
