using App.GitHealth.Api.Persistence;
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
        var analysisId = await repository.StartAsync(
            PersistenceTestData.PrimaryTarget(projectId),
            Start,
            CancellationToken.None);
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
        var successfulId = await CompleteAsync(
            repository,
            PersistenceTestData.PrimaryTarget(projectId),
            Start);
        var interruptedId = await repository.StartAsync(
            PersistenceTestData.PrimaryTarget(projectId),
            Start.AddHours(1),
            CancellationToken.None);

        Assert.Equal(successfulId, (await repository.GetLastSuccessfulAsync(
            projectId,
            CancellationToken.None))!.Id);
        var failure = new AnalysisFailure("git.timeout", "Timed out", Start.AddHours(2));
        await repository.FailAsync(interruptedId, failure, CancellationToken.None);

        Assert.Equal(successfulId, (await repository.GetLastSuccessfulAsync(
            projectId,
            CancellationToken.None))!.Id);
    }

    [Fact]
    public async Task SuccessfulAnalysisRestoresProjectAccessibility()
    {
        await using var database = await CreateDatabaseWithProjectAsync();
        var projectId = await ReadProjectIdAsync(database);
        await using var scope = database.CreateScope();
        var projects = scope.ServiceProvider.GetRequiredService<IProjectRepository>();
        var analyses = scope.ServiceProvider.GetRequiredService<IAnalysisRepository>();
        await projects.MarkUnavailableAsync(projectId, Start, CancellationToken.None);

        await CompleteAsync(
            analyses,
            PersistenceTestData.PrimaryTarget(projectId),
            Start.AddHours(1));

        var project = await projects.GetAsync(projectId, CancellationToken.None);
        Assert.True(project!.IsRepositoryAccessible);
        Assert.Equal(Start.AddHours(1).AddMinutes(2), project.UpdatedAtUtc);
    }

    [Fact]
    public async Task FailedBatchRollsBackSnapshotsAndLastSuccessPromotion()
    {
        await using var database = await CreateDatabaseWithProjectAsync();
        var projectId = await ReadProjectIdAsync(database);
        await using var scope = database.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IAnalysisRepository>();
        var target = PersistenceTestData.PrimaryTarget(projectId);
        var successfulId = await CompleteAsync(repository, target, Start);
        var failedId = await repository.StartAsync(
            target,
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

    [Fact]
    public async Task AnalysesOfDifferentBaselinesEachKeepTheirOwnLatest()
    {
        await using var database = await CreateDatabaseWithProjectAsync();
        var projectId = await ReadProjectIdAsync(database);
        await using var scope = database.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IAnalysisRepository>();
        var primary = PersistenceTestData.PrimaryTarget(projectId);
        var secondary = PersistenceTestData.SecondaryTarget(projectId);

        var primaryId = await CompleteAsync(repository, primary, Start);
        var secondaryId = await CompleteAsync(repository, secondary, Start.AddHours(1));

        Assert.Equal(primaryId, await ReadBaselineLatestAsync(repository, primary));
        Assert.Equal(secondaryId, await ReadBaselineLatestAsync(repository, secondary));
        // The secondary run finished last; the project still speaks for its primary baseline.
        Assert.Equal(primaryId, (await repository.GetLastSuccessfulAsync(
            projectId,
            CancellationToken.None))!.Id);
    }

    [Fact]
    public async Task DeletingAnAnalysisRepointsItsBaselineToThePreviousCapture()
    {
        await using var database = await CreateDatabaseWithProjectAsync();
        var projectId = await ReadProjectIdAsync(database);
        await using var scope = database.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IAnalysisRepository>();
        var target = PersistenceTestData.PrimaryTarget(projectId);
        var previousId = await CompleteAsync(repository, target, Start);
        var latestId = await CompleteAsync(repository, target, Start.AddHours(1));

        var result = await repository.DeleteAsync(latestId, CancellationToken.None);

        Assert.True(result.WasFound);
        Assert.False(result.WasRunning);
        Assert.Null(await repository.GetAsync(latestId, CancellationToken.None));
        Assert.Equal(previousId, await ReadBaselineLatestAsync(repository, target));
        Assert.Equal(previousId, (await repository.GetLastSuccessfulAsync(
            projectId,
            CancellationToken.None))!.Id);
    }

    [Fact]
    public async Task DeletingAnAnalysisCascadesToBranchesAndContributors()
    {
        await using var database = await CreateDatabaseWithProjectAsync();
        var projectId = await ReadProjectIdAsync(database);
        await using var scope = database.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IAnalysisRepository>();
        var target = PersistenceTestData.PrimaryTarget(projectId);
        await CompleteAsync(repository, target, Start);
        var latestId = await CompleteAsync(repository, target, Start.AddHours(1));
        var before = await CountRowsAsync(database);

        await repository.DeleteAsync(latestId, CancellationToken.None);

        Assert.Equal(new RowCounts(2, 2, 2, 2), before);
        Assert.Equal(new RowCounts(2, 1, 1, 1), await CountRowsAsync(database));
    }

    [Fact]
    public async Task DeletingARunningAnalysisIsRefused()
    {
        await using var database = await CreateDatabaseWithProjectAsync();
        var projectId = await ReadProjectIdAsync(database);
        await using var scope = database.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IAnalysisRepository>();
        var runningId = await repository.StartAsync(
            PersistenceTestData.PrimaryTarget(projectId),
            Start,
            CancellationToken.None);

        var result = await repository.DeleteAsync(runningId, CancellationToken.None);

        Assert.True(result.WasFound);
        Assert.True(result.WasRunning);
        var stored = await repository.GetAsync(runningId, CancellationToken.None);
        Assert.Equal(AnalysisRunStatus.Running, stored!.Status);
    }

    [Fact]
    public async Task DeletingTheOnlyCaptureLeavesTheBaselineWithoutOne()
    {
        await using var database = await CreateDatabaseWithProjectAsync();
        var projectId = await ReadProjectIdAsync(database);
        await using var scope = database.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IAnalysisRepository>();
        var target = PersistenceTestData.PrimaryTarget(projectId);
        var onlyId = await CompleteAsync(repository, target, Start);

        await repository.DeleteAsync(onlyId, CancellationToken.None);

        Assert.Null(await repository.GetLastSuccessfulForBaselineAsync(
            target,
            CancellationToken.None));
        Assert.Null(await repository.GetLastSuccessfulAsync(projectId, CancellationToken.None));
    }

    [Fact]
    public async Task DeletingAProjectRemovesItsBaselinesAndRuns()
    {
        await using var database = await CreateDatabaseWithProjectAsync();
        var projectId = await ReadProjectIdAsync(database);
        await using var scope = database.CreateScope();
        var analyses = scope.ServiceProvider.GetRequiredService<IAnalysisRepository>();
        await CompleteAsync(analyses, PersistenceTestData.PrimaryTarget(projectId), Start);
        await CompleteAsync(analyses, PersistenceTestData.SecondaryTarget(projectId), Start);

        var deleted = await scope.ServiceProvider.GetRequiredService<IProjectRepository>()
            .DeleteAsync(projectId, CancellationToken.None);

        Assert.True(deleted);
        Assert.Equal(new RowCounts(0, 0, 0, 0), await CountRowsAsync(database));
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

    private static async Task<Guid> ReadBaselineLatestAsync(
        IAnalysisRepository repository,
        AnalysisTarget target)
    {
        var latest = await repository.GetLastSuccessfulForBaselineAsync(
            target,
            CancellationToken.None);
        return latest!.Id;
    }

    private static async Task<Guid> CompleteAsync(
        IAnalysisRepository repository,
        AnalysisTarget target,
        DateTimeOffset startedAt)
    {
        var analysisId = await repository.StartAsync(target, startedAt, CancellationToken.None);
        var completion = new AnalysisCompletion(
            PersistenceTestData.CreateScan(startedAt.AddMinutes(1)),
            startedAt.AddMinutes(2));
        await repository.CompleteAsync(analysisId, completion, CancellationToken.None);
        return analysisId;
    }

    private static async Task<RowCounts> CountRowsAsync(SqliteTestDatabase database)
    {
        await using var scope = database.CreateScope();
        var factory = scope.ServiceProvider
            .GetRequiredService<IDbContextFactory<GitHealthDbContext>>();
        await using var context = await factory.CreateDbContextAsync();
        return new RowCounts(
            await context.ProjectBaselines.CountAsync(),
            await context.AnalysisRuns.CountAsync(),
            await context.BranchSnapshots.CountAsync(),
            await context.ContributorSnapshots.CountAsync());
    }

    private sealed record RowCounts(int Baselines, int Runs, int Branches, int Contributors);
}
