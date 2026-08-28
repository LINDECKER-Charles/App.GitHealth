using App.GitHealth.Api.Persistence.Models;
using App.GitHealth.Api.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace App.GitHealth.Api.Tests.Persistence;

public sealed class AnalysisPersistenceTests
{
    private static readonly DateTimeOffset Start =
        new(2026, 8, 10, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task CompletionPersistsUtcFactsAndContributorsInOneSnapshot()
    {
        await using var database = await CreateDatabaseWithProjectAsync();
        var projectId = await ReadProjectIdAsync(database);
        await using var scope = database.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IAnalysisRepository>();
        var analysisId = await repository.StartAsync(projectId, Start, CancellationToken.None);
        var completion = new AnalysisCompletion(
            PersistenceTestData.CreateScan(Start.AddMinutes(2)),
            Start.AddMinutes(3));

        await repository.CompleteAsync(analysisId, completion, CancellationToken.None);

        var stored = await repository.GetAsync(analysisId, CancellationToken.None);
        Assert.Equal(AnalysisRunStatus.Completed, stored!.Status);
        Assert.Equal("refs/heads/main", stored.ReferenceName);
        Assert.Equal(TimeSpan.Zero, stored.CapturedAtUtc!.Value.Offset);
        var branch = Assert.Single(stored.Branches);
        Assert.Equal("refs/remotes/origin/feature/café", branch.ReferenceName);
        Assert.Equal(TimeSpan.Zero, branch.LastActivityAtUtc!.Value.Offset);
        Assert.Equal(2, Assert.Single(branch.Contributors).CommitCount);
    }

    [Fact]
    public async Task RunningOrFailedAnalysisNeverReplacesLastSuccess()
    {
        await using var database = await CreateDatabaseWithProjectAsync();
        var projectId = await ReadProjectIdAsync(database);
        await using var scope = database.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IAnalysisRepository>();
        var successfulId = await CompleteAsync(repository, projectId, Start);
        var interruptedId = await repository.StartAsync(
            projectId,
            Start.AddHours(1),
            CancellationToken.None);

        Assert.Equal(successfulId, (await repository.GetLastSuccessfulAsync(
            projectId,
            CancellationToken.None))!.Id);
        var failure = new AnalysisFailure("git.timeout", "Délai dépassé", Start.AddHours(2));
        await repository.FailAsync(interruptedId, failure, CancellationToken.None);

        Assert.Equal(successfulId, (await repository.GetLastSuccessfulAsync(
            projectId,
            CancellationToken.None))!.Id);
    }

    [Fact]
    public async Task FailedBatchRollsBackSnapshotsAndLastSuccessPromotion()
    {
        await using var database = await CreateDatabaseWithProjectAsync();
        var projectId = await ReadProjectIdAsync(database);
        await using var scope = database.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IAnalysisRepository>();
        var successfulId = await CompleteAsync(repository, projectId, Start);
        var failedId = await repository.StartAsync(
            projectId,
            Start.AddHours(1),
            CancellationToken.None);
        var duplicatedRef = "refs/heads/duplicate";
        var completion = new AnalysisCompletion(
            PersistenceTestData.CreateScan(Start.AddHours(1), duplicatedRef, duplicatedRef),
            Start.AddHours(2));

        await Assert.ThrowsAsync<DbUpdateException>(() => repository.CompleteAsync(
            failedId,
            completion,
            CancellationToken.None));

        var failed = await repository.GetAsync(failedId, CancellationToken.None);
        var last = await repository.GetLastSuccessfulAsync(projectId, CancellationToken.None);
        Assert.Equal(AnalysisRunStatus.Running, failed!.Status);
        Assert.Empty(failed.Branches);
        Assert.Equal(successfulId, last!.Id);
    }

    private static async Task<SqliteTestDatabase> CreateDatabaseWithProjectAsync()
    {
        var database = await SqliteTestDatabase.CreateAsync();
        await using var scope = database.CreateScope();
        var path = Path.Combine(database.RootPath, "repository");
        var project = PersistenceTestData.CreateProject(path);
        await scope.ServiceProvider.GetRequiredService<IProjectRepository>()
            .AddAsync(project, Start, CancellationToken.None);
        return database;
    }

    private static async Task<Guid> ReadProjectIdAsync(SqliteTestDatabase database)
    {
        await using var scope = database.CreateScope();
        var projects = await scope.ServiceProvider.GetRequiredService<IProjectRepository>()
            .ListAsync(CancellationToken.None);
        return Assert.Single(projects).Id;
    }

    private static async Task<Guid> CompleteAsync(
        IAnalysisRepository repository,
        Guid projectId,
        DateTimeOffset startedAt)
    {
        var analysisId = await repository.StartAsync(projectId, startedAt, CancellationToken.None);
        var completion = new AnalysisCompletion(
            PersistenceTestData.CreateScan(startedAt.AddMinutes(1)),
            startedAt.AddMinutes(2));
        await repository.CompleteAsync(analysisId, completion, CancellationToken.None);
        return analysisId;
    }
}
