using System.Net;
using System.Net.Http.Json;
using App.GitHealth.Api.Persistence;
using App.GitHealth.Api.Persistence.Models;
using App.GitHealth.Api.Tests.Hosting;
using App.GitHealth.Api.Tests.Persistence;
using App.GitHealth.Core.Analysis;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace App.GitHealth.Api.Tests.Analyses;

public sealed class AnalysisQueueEndpointTests
{
    private const int StatusPollAttempts = 100;
    private static readonly TimeSpan TestTimeout = TimeSpan.FromSeconds(10);

    [Fact]
    public async Task FullQueuePersistsFailedRunInsteadOfLeavingItRunning()
    {
        using var repository = GitTestRepository.Create(aheadBranchCount: 0);
        var scanner = CreateScanner();
        using var factory = CreateFactory(repository.RootPath, scanner, capacity: 1);
        using var client = factory.CreateClient();
        var projects = await CreateProjectsAsync(client, repository.RootPath, count: 3);

        var active = await ApiTestWorkflow.LaunchAsync(client, projects[0]);
        using var activeResponse = active.Response;
        await scanner.Started.WaitAsync(TestTimeout);
        var waiting = await ApiTestWorkflow.LaunchAsync(client, projects[1]);
        using var waitingResponse = waiting.Response;
        await ChangeWaitingProjectSettingsAsync(client, projects[1]);
        using var rejected = await client.PostAsync(
            $"/api/projects/{projects[2]}/analyses",
            content: null);

        Assert.Equal(HttpStatusCode.ServiceUnavailable, rejected.StatusCode);
        Assert.Equal("analysis.queue_full", await ApiTestWorkflow.ReadProblemCodeAsync(rejected));
        await AssertProjectRunStatusAsync(factory, projects[2], AnalysisRunStatus.Failed);
        scanner.Release();
        await ApiTestWorkflow.WaitForStatusAsync(client, active.Id, "Completed");
        await ApiTestWorkflow.WaitForStatusAsync(client, waiting.Id, "Completed");
        AssertFrozenSettings(scanner, Path.Combine(repository.RootPath, "controlled-1"));
    }

    [Fact]
    public async Task HostShutdownCancelsActiveAndWaitingRuns()
    {
        using var repository = GitTestRepository.Create(aheadBranchCount: 0);
        var scanner = CreateScanner();
        var factory = CreateFactory(repository.RootPath, scanner, capacity: 2);
        var client = factory.CreateClient();
        var projects = await CreateProjectsAsync(client, repository.RootPath, count: 2);
        var active = await ApiTestWorkflow.LaunchAsync(client, projects[0]);
        active.Response.Dispose();
        await scanner.Started.WaitAsync(TestTimeout);
        var waiting = await ApiTestWorkflow.LaunchAsync(client, projects[1]);
        waiting.Response.Dispose();

        client.Dispose();
        using var timeout = new CancellationTokenSource(TestTimeout);
        await factory.StopHostAsync(timeout.Token);
        await AssertStoredStatusesAsync(
            factory.DatabasePath,
            [active.Id, waiting.Id],
            AnalysisRunStatus.Cancelled.ToString());
        factory.Dispose();
    }

    private static ApiApplicationFactory CreateFactory(
        string repositoriesRoot,
        ControlledRepositoryScanner scanner,
        int capacity) => new()
        {
            RepositoriesRoot = repositoriesRoot,
            QueueCapacity = capacity,
            TestServices = services => ReplaceScanner(services, scanner),
        };

    private static ControlledRepositoryScanner CreateScanner() => new(
        PersistenceTestData.CreateScan(
            DateTimeOffset.UtcNow,
            "refs/heads/feature/controlled"));

    private static void ReplaceScanner(
        IServiceCollection services,
        IRepositoryScanner scanner)
    {
        services.RemoveAll<IRepositoryScanner>();
        services.AddSingleton(scanner);
    }

    private static async Task<Guid[]> CreateProjectsAsync(
        HttpClient client,
        string rootPath,
        int count)
    {
        var projects = new List<Guid>();
        for (var index = 0; index < count; index++)
        {
            var path = Path.Combine(rootPath, $"controlled-{index}");
            projects.Add(await ApiTestWorkflow.CreateProjectAsync(client, path));
        }

        return projects.ToArray();
    }

    private static async Task AssertProjectRunStatusAsync(
        ApiApplicationFactory factory,
        Guid projectId,
        AnalysisRunStatus expected)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<GitHealthDbContext>();
        var status = await context.AnalysisRuns.AsNoTracking()
            .Where(analysis => analysis.ProjectId == projectId)
            .Select(analysis => analysis.Status)
            .SingleAsync();
        Assert.Equal(expected, status);
    }

    private static async Task ChangeWaitingProjectSettingsAsync(
        HttpClient client,
        Guid projectId)
    {
        var request = new
        {
            referenceName = "refs/heads/develop",
            branchNamespace = "refs/heads/changed/*",
            activeUntilDays = 10,
            inactiveAfterDays = 20,
            excludedPatterns = Array.Empty<string>(),
            protectedPatterns = Array.Empty<string>(),
        };
        using var response = await client.PutAsJsonAsync(
            $"/api/projects/{projectId}/settings",
            request);
        response.EnsureSuccessStatusCode();
    }

    private static void AssertFrozenSettings(
        ControlledRepositoryScanner scanner,
        string repositoryPath)
    {
        var request = scanner.Requests.Single(candidate =>
            candidate.RepositoryPath == Path.GetFullPath(repositoryPath));
        Assert.Equal("refs/heads/main", request.Reference.FullName);
        Assert.Equal("refs/heads/feature/*", request.BranchPattern);
    }

    private static async Task AssertStoredStatusesAsync(
        string databasePath,
        IReadOnlyList<Guid> analysisIds,
        string expected)
    {
        for (var attempt = 0; attempt < StatusPollAttempts; attempt++)
        {
            var statuses = await ReadStatusesAsync(databasePath, analysisIds);
            if (statuses.Count == analysisIds.Count
                && statuses.All(status => status == expected))
            {
                return;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(25));
        }

        throw new TimeoutException("Les analyses n’ont pas été annulées à l’arrêt.");
    }

    private static async Task<IReadOnlyList<string>> ReadStatusesAsync(
        string databasePath,
        IReadOnlyList<Guid> analysisIds)
    {
        await using var connection = new SqliteConnection($"Data Source={databasePath}");
        await connection.OpenAsync();
        var statuses = new List<string>();
        foreach (var analysisId in analysisIds)
        {
            statuses.Add(await ReadStatusAsync(connection, analysisId));
        }

        return statuses;
    }

    private static async Task<string> ReadStatusAsync(
        SqliteConnection connection,
        Guid analysisId)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT Status FROM AnalysisRuns WHERE Id = $id;";
        command.Parameters.AddWithValue("$id", analysisId);
        return (string)(await command.ExecuteScalarAsync()
            ?? throw new InvalidOperationException("Analyse absente de SQLite."));
    }
}
