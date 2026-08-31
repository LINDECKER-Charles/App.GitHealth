using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using App.GitHealth.Api.Tests.Hosting;
using App.GitHealth.Api.Tests.Persistence;
using App.GitHealth.Core.Analysis;
using App.GitHealth.Core.Branches;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace App.GitHealth.Api.Tests.Analyses;

public sealed class AnalysisEndpointTests
{
    private static readonly TimeSpan TestTimeout = TimeSpan.FromSeconds(10);

    [Fact]
    public async Task LaunchReturnsLocationAndDeduplicatesWhileScanIsBlocked()
    {
        using var repository = GitTestRepository.Create(aheadBranchCount: 0);
        var scanner = CreateControlledScanner(repository.RepositoryPath);
        using var factory = CreateFactory(repository.RootPath, scanner);
        using var client = factory.CreateClient();
        var projectId = await ApiTestWorkflow.CreateProjectAsync(
            client,
            repository.RepositoryPath);

        var first = await ApiTestWorkflow.LaunchAsync(client, projectId);
        using var firstResponse = first.Response;
        AssertAccepted(firstResponse, first.Id);
        await scanner.Started.WaitAsync(TestTimeout);
        await AssertPhaseAsync(client, first.Id, "Topology");

        var duplicate = await ApiTestWorkflow.LaunchAsync(client, projectId);
        using var duplicateResponse = duplicate.Response;
        Assert.Equal(first.Id, duplicate.Id);
        AssertAccepted(duplicateResponse, duplicate.Id);
        Assert.True(duplicate.IsDuplicate);
        scanner.AdvanceToEnrichment();
        await scanner.EnrichmentStarted.WaitAsync(TestTimeout);
        await AssertPhaseAsync(client, first.Id, "Enrichment");
        scanner.Release();
        await ApiTestWorkflow.WaitForStatusAsync(client, first.Id, "Completed");
    }

    [Fact]
    public async Task AnalysisStopsAtTheConfiguredGlobalTimeout()
    {
        using var repository = GitTestRepository.Create(aheadBranchCount: 0);
        var scanner = CreateControlledScanner(repository.RepositoryPath);
        using var factory = new ApiApplicationFactory
        {
            RepositoriesRoot = repository.RootPath,
            AnalysisTimeoutSeconds = 1,
            TestServices = services => ReplaceScanner(services, scanner),
        };
        using var client = factory.CreateClient();
        var projectId = await ApiTestWorkflow.CreateProjectAsync(
            client,
            repository.RepositoryPath);

        var launch = await ApiTestWorkflow.LaunchAsync(client, projectId);
        using var response = launch.Response;
        var status = await ApiTestWorkflow.WaitForStatusAsync(client, launch.Id, "Failed");

        Assert.Equal("analysis.timed_out", status.GetProperty("failureCode").GetString());
        scanner.Release();
    }

    [Fact]
    public async Task AnalysisRevalidatesGitMetadataBeforeScanning()
    {
        using var repository = GitTestRepository.Create(aheadBranchCount: 0);
        using var outside = GitTestRepository.Create(aheadBranchCount: 0);
        var scanner = new RelocatedMetadataScanner(
            repository.RepositoryPath,
            outside.RepositoryPath);
        using var factory = new ApiApplicationFactory
        {
            RepositoriesRoot = repository.RootPath,
            TestServices = services => ReplaceScanner(services, scanner),
        };
        using var client = factory.CreateClient();
        var projectId = await ApiTestWorkflow.CreateProjectAsync(
            client,
            repository.RepositoryPath);

        var launch = await ApiTestWorkflow.LaunchAsync(client, projectId);
        using var response = launch.Response;
        var status = await ApiTestWorkflow.WaitForStatusAsync(client, launch.Id, "Failed");

        Assert.Equal(
            "repository.path_not_allowed",
            status.GetProperty("failureCode").GetString());
        Assert.False(scanner.WasScanned);
    }

    private static ApiApplicationFactory CreateFactory(
        string repositoriesRoot,
        ControlledRepositoryScanner scanner) => new()
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

    private static ControlledRepositoryScanner CreateControlledScanner(string repositoryPath)
    {
        var reference = new GitRef("refs/heads/main");
        var location = new RepositoryLocation(
            repositoryPath,
            Path.Combine(repositoryPath, ".git"),
            repositoryPath);
        var descriptor = new RepositoryDescriptor(location, reference, [reference]);
        var scan = PersistenceTestData.CreateScan(
            DateTimeOffset.UtcNow,
            "refs/heads/feature/controlled");
        return new ControlledRepositoryScanner(scan, descriptor);
    }

    private static void AssertAccepted(HttpResponseMessage response, Guid analysisId)
    {
        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        Assert.Equal($"/api/analyses/{analysisId}", response.Headers.Location?.OriginalString);
    }

    private static async Task AssertPhaseAsync(
        HttpClient client,
        Guid analysisId,
        string expectedPhase)
    {
        var status = await client.GetFromJsonAsync<JsonElement>(
            $"/api/analyses/{analysisId}");
        Assert.Equal(expectedPhase, status.GetProperty("phase").GetString());
    }

    private sealed class RelocatedMetadataScanner(
        string repositoryPath,
        string outsidePath) : IRepositoryScanner
    {
        private int _inspectionCount;

        public bool WasScanned { get; private set; }

        public Task<RepositoryResult<RepositoryDescriptor>> InspectAsync(
            string path,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var gitDirectory = Interlocked.Increment(ref _inspectionCount) == 1
                ? Path.Combine(repositoryPath, ".git")
                : Path.Combine(outsidePath, ".git");
            var reference = new GitRef("refs/heads/main");
            var location = new RepositoryLocation(repositoryPath, gitDirectory, repositoryPath);
            var descriptor = new RepositoryDescriptor(location, reference, [reference]);
            return Task.FromResult(RepositoryResults.Success(descriptor));
        }

        public Task<RepositoryResult<bool>> ContainsCommitAsync(
            string path,
            CommitId commit,
            CancellationToken cancellationToken)
        {
            _ = path;
            _ = commit;
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(RepositoryResults.Success(true));
        }

        public Task<RepositoryResult<RepositoryScan>> ScanAsync(
            RepositoryScanRequest request,
            CancellationToken cancellationToken)
        {
            WasScanned = true;
            throw new InvalidOperationException("The scan must not be started.");
        }
    }
}
