using App.GitHealth.Api.Tests.Hosting;
using App.GitHealth.Api.Tests.Persistence;
using App.GitHealth.Core.Analysis;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace App.GitHealth.Api.Tests.Analyses;

public sealed class ParallelAnalysisTests
{
    private const int QueueCapacity = 8;
    private static readonly TimeSpan TestTimeout = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan SequentialObservation = TimeSpan.FromMilliseconds(250);

    [Fact]
    public async Task ParallelHostScansSeveralProjectsAtOnce()
    {
        const int parallelAnalyses = 3;
        using var repository = GitTestRepository.Create(aheadBranchCount: 0);
        var scanner = CreateScanner();
        using var factory = CreateFactory(repository.RootPath, scanner, parallelAnalyses);
        using var client = factory.CreateClient();
        var projects = await CreateProjectsAsync(client, repository.RootPath, parallelAnalyses);

        var analyses = await LaunchAllAsync(client, projects);
        await scanner.ReachedConcurrency(parallelAnalyses).WaitAsync(TestTimeout);
        scanner.Release();
        await WaitForCompletionAsync(client, analyses);

        Assert.Equal(parallelAnalyses, scanner.PeakConcurrency);
    }

    [Fact]
    public async Task SequentialHostScansOneProjectAtATime()
    {
        using var repository = GitTestRepository.Create(aheadBranchCount: 0);
        var scanner = CreateScanner();
        using var factory = CreateFactory(repository.RootPath, scanner, parallelAnalyses: 1);
        using var client = factory.CreateClient();
        var projects = await CreateProjectsAsync(client, repository.RootPath, count: 2);

        var analyses = await LaunchAllAsync(client, projects);
        await scanner.ReachedConcurrency(1).WaitAsync(TestTimeout);
        // The second analysis stays queued while the first one is not released.
        await Task.Delay(SequentialObservation);
        var observedWhileBlocked = scanner.PeakConcurrency;
        scanner.Release();
        await WaitForCompletionAsync(client, analyses);

        Assert.Equal(1, observedWhileBlocked);
        Assert.Equal(1, scanner.PeakConcurrency);
    }

    private static ApiApplicationFactory CreateFactory(
        string repositoriesRoot,
        ControlledRepositoryScanner scanner,
        int parallelAnalyses) => new()
        {
            RepositoriesRoot = repositoriesRoot,
            QueueCapacity = QueueCapacity,
            MaximumParallelAnalyses = parallelAnalyses,
            TestServices = services =>
            {
                services.RemoveAll<IRepositoryScanner>();
                services.AddSingleton<IRepositoryScanner>(scanner);
            },
        };

    private static ControlledRepositoryScanner CreateScanner() => new(
        PersistenceTestData.CreateScan(DateTimeOffset.UtcNow, "refs/heads/feature/parallel"));

    private static async Task<Guid[]> CreateProjectsAsync(
        HttpClient client,
        string rootPath,
        int count)
    {
        var projects = new List<Guid>();
        for (var index = 0; index < count; index++)
        {
            var path = Path.Combine(rootPath, $"parallel-{index}");
            projects.Add(await ApiTestWorkflow.CreateProjectAsync(client, path));
        }

        return projects.ToArray();
    }

    private static async Task<Guid[]> LaunchAllAsync(
        HttpClient client,
        Guid[] projects)
    {
        var analyses = new List<Guid>(projects.Length);
        foreach (var projectId in projects)
        {
            var launch = await ApiTestWorkflow.LaunchAsync(client, projectId);
            launch.Response.Dispose();
            analyses.Add(launch.Id);
        }

        return analyses.ToArray();
    }

    private static async Task WaitForCompletionAsync(
        HttpClient client,
        IReadOnlyList<Guid> analyses)
    {
        foreach (var analysisId in analyses)
        {
            await ApiTestWorkflow.WaitForStatusAsync(client, analysisId, "Completed");
        }
    }
}
