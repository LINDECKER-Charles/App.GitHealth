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
}
