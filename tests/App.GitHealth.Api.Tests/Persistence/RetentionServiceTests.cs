using App.GitHealth.Api.Persistence.Models;
using App.GitHealth.Api.Persistence.Repositories;
using App.GitHealth.Api.Persistence.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace App.GitHealth.Api.Tests.Persistence;

public sealed class RetentionServiceTests
{
    private static readonly DateTimeOffset Now =
        new(2026, 8, 28, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task RetentionIsDisabledByDefault()
    {
        await using var database = await CreateDatabaseWithProjectAsync();
        var analysisId = await CompleteAnalysisAsync(database, Now.AddDays(-100));
        await using var scope = database.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IRetentionService>();

        var result = await service.ApplyAsync(Now, CancellationToken.None);

        Assert.False(result.IsEnabled);
        Assert.Equal(0, result.DeletedAnalysisCount);
        Assert.NotNull(await scope.ServiceProvider.GetRequiredService<IAnalysisRepository>()
            .GetAsync(analysisId, CancellationToken.None));
    }

    [Fact]
    public async Task RetentionDeletesOldHistoryButPreservesLatestSuccessfulSnapshot()
    {
        await using var database = await CreateDatabaseWithProjectAsync(30);
        var oldId = await CompleteAnalysisAsync(database, Now.AddDays(-100));
        var latestId = await CompleteAnalysisAsync(database, Now.AddDays(-1));
        await using var scope = database.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IRetentionService>();

        var result = await service.ApplyAsync(Now, CancellationToken.None);

        var repository = scope.ServiceProvider.GetRequiredService<IAnalysisRepository>();
        Assert.True(result.IsEnabled);
        Assert.Equal(1, result.DeletedAnalysisCount);
        Assert.Null(await repository.GetAsync(oldId, CancellationToken.None));
        Assert.Equal(latestId, (await repository.GetLastSuccessfulAsync(
            await ReadProjectIdAsync(database),
            CancellationToken.None))!.Id);
        Assert.Equal(1, await CountBranchesAsync(database));
    }

    private static async Task<SqliteTestDatabase> CreateDatabaseWithProjectAsync(
        int? retentionDays = null)
    {
        var database = await SqliteTestDatabase.CreateAsync(retentionDays);
        await using var scope = database.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IProjectRepository>();
        var path = Path.Combine(database.RootPath, "repository");
        var project = PersistenceTestData.CreateProject(path);
        await repository.AddAsync(project, Now.AddDays(-120), CancellationToken.None);
        return database;
    }

    private static async Task<Guid> CompleteAnalysisAsync(
        SqliteTestDatabase database,
        DateTimeOffset startedAt)
    {
        await using var scope = database.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IAnalysisRepository>();
        var projectId = await ReadProjectIdAsync(database);
        var id = await repository.StartAsync(projectId, startedAt, CancellationToken.None);
        var completion = new AnalysisCompletion(
            PersistenceTestData.CreateScan(startedAt.AddMinutes(1)),
            startedAt.AddMinutes(2));
        await repository.CompleteAsync(id, completion, CancellationToken.None);
        return id;
    }

    private static async Task<Guid> ReadProjectIdAsync(SqliteTestDatabase database)
    {
        await using var scope = database.CreateScope();
        var projects = await scope.ServiceProvider.GetRequiredService<IProjectRepository>()
            .ListAsync(CancellationToken.None);
        return Assert.Single(projects).Id;
    }

    private static async Task<int> CountBranchesAsync(SqliteTestDatabase database)
    {
        await using var scope = database.CreateScope();
        var factory = scope.ServiceProvider.GetRequiredService<
            IDbContextFactory<App.GitHealth.Api.Persistence.GitHealthDbContext>>();
        await using var context = await factory.CreateDbContextAsync();
        return await context.BranchSnapshots.CountAsync();
    }
}
