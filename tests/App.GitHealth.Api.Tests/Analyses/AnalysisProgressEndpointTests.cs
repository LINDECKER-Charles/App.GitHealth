using System.Net.Http.Json;
using System.Text.Json;
using App.GitHealth.Api.Tests.Hosting;
using App.GitHealth.Api.Tests.Persistence;
using App.GitHealth.Core.Analysis;
using App.GitHealth.Core.Branches;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace App.GitHealth.Api.Tests.Analyses;

/// <summary>
/// What a reader watching a run gets: the ledger of its references and the Git commands it
/// has run, next to the phase.
/// </summary>
public sealed class AnalysisProgressEndpointTests
{
    private const string ScannedReference = "refs/heads/feature/controlled";
    private static readonly TimeSpan TestTimeout = TimeSpan.FromSeconds(10);

    [Fact]
    public async Task RunningAnalysisReportsItsLedgerAndItsCommands()
    {
        using var repository = GitTestRepository.Create(aheadBranchCount: 0);
        var scanner = CreateScanner(repository.RepositoryPath);
        using var factory = CreateFactory(repository.RootPath, scanner);
        using var client = factory.CreateClient();
        var projectId = await ApiTestWorkflow.CreateProjectAsync(
            client,
            repository.RepositoryPath);

        var launch = await ApiTestWorkflow.LaunchAsync(client, projectId);
        using var response = launch.Response;
        await scanner.Started.WaitAsync(TestTimeout);

        var progress = await ReadProgressAsync(client, launch.Id);
        var reference = Assert.Single(progress.GetProperty("references").EnumerateArray());
        Assert.Equal(ScannedReference, reference.GetProperty("referenceName").GetString());
        Assert.Equal("Measured", reference.GetProperty("state").GetString());
        Assert.Equal("Diverged", reference.GetProperty("topology").GetString());
        Assert.Equal(2, reference.GetProperty("aheadCount").GetInt32());

        var command = Assert.Single(progress.GetProperty("commands").EnumerateArray());
        Assert.Equal(
            $"git merge-base main {ScannedReference}",
            command.GetProperty("commandLine").GetString());
        Assert.Equal(1, command.GetProperty("sequence").GetInt32());
        Assert.Equal(1, progress.GetProperty("commandCount").GetInt32());

        scanner.Release();
        await ApiTestWorkflow.WaitForStatusAsync(client, launch.Id, "Completed");
    }

    [Fact]
    public async Task ContributorsLandOnTheLedgerDuringTheSecondStage()
    {
        using var repository = GitTestRepository.Create(aheadBranchCount: 0);
        var scanner = CreateScanner(repository.RepositoryPath);
        using var factory = CreateFactory(repository.RootPath, scanner);
        using var client = factory.CreateClient();
        var projectId = await ApiTestWorkflow.CreateProjectAsync(
            client,
            repository.RepositoryPath);

        var launch = await ApiTestWorkflow.LaunchAsync(client, projectId);
        using var response = launch.Response;
        await scanner.Started.WaitAsync(TestTimeout);
        scanner.AdvanceToEnrichment();
        await scanner.EnrichmentStarted.WaitAsync(TestTimeout);

        var progress = await ReadProgressAsync(client, launch.Id);
        var reference = Assert.Single(progress.GetProperty("references").EnumerateArray());
        Assert.Equal("Read", reference.GetProperty("state").GetString());
        Assert.Equal("Ada Lovelace", reference.GetProperty("topContributor").GetString());

        scanner.Release();
        await ApiTestWorkflow.WaitForStatusAsync(client, launch.Id, "Completed");
    }

    /// <summary>
    /// A run ends between two polls: the answer that announces the end has to carry the
    /// complete reading, or a reader is left with a half-filled ledger under a finished
    /// header.
    /// </summary>
    [Fact]
    public async Task FinishedAnalysisStillCarriesItsCompletedLedger()
    {
        using var repository = GitTestRepository.Create(aheadBranchCount: 0);
        var scanner = CreateScanner(repository.RepositoryPath);
        using var factory = CreateFactory(repository.RootPath, scanner);
        using var client = factory.CreateClient();
        var projectId = await ApiTestWorkflow.CreateProjectAsync(
            client,
            repository.RepositoryPath);

        var launch = await ApiTestWorkflow.LaunchAsync(client, projectId);
        using var response = launch.Response;
        scanner.Release();
        var status = await ApiTestWorkflow.WaitForStatusAsync(client, launch.Id, "Completed");

        var progress = status.GetProperty("progress");
        Assert.Equal(JsonValueKind.Object, progress.ValueKind);
        var reference = Assert.Single(progress.GetProperty("references").EnumerateArray());
        Assert.Equal("Read", reference.GetProperty("state").GetString());
        Assert.Equal("Ada Lovelace", reference.GetProperty("topContributor").GetString());
    }

    private static async Task<JsonElement> ReadProgressAsync(HttpClient client, Guid analysisId)
    {
        var status = await client.GetFromJsonAsync<JsonElement>($"/api/analyses/{analysisId}");
        var progress = status.GetProperty("progress");
        Assert.Equal(JsonValueKind.Object, progress.ValueKind);
        return progress;
    }

    private static ApiApplicationFactory CreateFactory(
        string repositoriesRoot,
        ControlledRepositoryScanner scanner) => new()
        {
            RepositoriesRoot = repositoriesRoot,
            TestServices = services =>
            {
                services.RemoveAll<IRepositoryScanner>();
                services.AddSingleton<IRepositoryScanner>(scanner);
            },
        };

    private static ControlledRepositoryScanner CreateScanner(string repositoryPath)
    {
        var reference = new GitRef("refs/heads/main");
        var location = new RepositoryLocation(
            repositoryPath,
            Path.Combine(repositoryPath, ".git"),
            repositoryPath);
        var descriptor = new RepositoryDescriptor(location, reference, [reference]);
        var scan = PersistenceTestData.CreateScan(DateTimeOffset.UtcNow, ScannedReference);
        return new ControlledRepositoryScanner(scan, descriptor);
    }
}
