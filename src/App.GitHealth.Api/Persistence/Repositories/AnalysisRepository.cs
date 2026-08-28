using App.GitHealth.Api.Persistence.Entities;
using App.GitHealth.Api.Persistence.Models;
using Microsoft.EntityFrameworkCore;

namespace App.GitHealth.Api.Persistence.Repositories;

internal sealed class AnalysisRepository(IDbContextFactory<GitHealthDbContext> contextFactory)
    : IAnalysisRepository
{
    public Task<Guid> StartAsync(
        Guid projectId,
        DateTimeOffset startedAtUtc,
        CancellationToken cancellationToken)
    {
        return SqliteWriteExecutor.ExecuteAsync(async () =>
        {
            await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
            var project = await FindProjectAsync(context, projectId, cancellationToken);
            var analysis = AnalysisRunEntity.Start(project, startedAtUtc);
            context.AnalysisRuns.Add(analysis);
            await context.SaveChangesAsync(cancellationToken);
            return analysis.Id;
        });
    }

    public Task CompleteAsync(
        Guid analysisId,
        AnalysisCompletion completion,
        CancellationToken cancellationToken)
    {
        return SqliteWriteExecutor.ExecuteAsync(async () =>
        {
            await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
            await using var transaction = await context.Database
                .BeginTransactionAsync(cancellationToken);
            var analysis = await FindAnalysisAsync(context, analysisId, cancellationToken);
            analysis.Complete(completion);
            analysis.Project.LastSuccessfulAnalysisId = analysis.Id;
            await context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return true;
        });
    }

    public Task FailAsync(
        Guid analysisId,
        AnalysisFailure failure,
        CancellationToken cancellationToken)
    {
        return SqliteWriteExecutor.ExecuteAsync(async () =>
        {
            await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
            var analysis = await FindAnalysisAsync(context, analysisId, cancellationToken);
            analysis.Fail(failure);
            await context.SaveChangesAsync(cancellationToken);
            return true;
        });
    }

    public async Task<AnalysisRunEntity?> GetAsync(
        Guid analysisId,
        CancellationToken cancellationToken)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        return await ReadQuery(context)
            .SingleOrDefaultAsync(analysis => analysis.Id == analysisId, cancellationToken);
    }

    public async Task<AnalysisRunEntity?> GetLastSuccessfulAsync(
        Guid projectId,
        CancellationToken cancellationToken)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var lastId = await context.Projects.AsNoTracking()
            .Where(project => project.Id == projectId)
            .Select(project => project.LastSuccessfulAnalysisId)
            .SingleOrDefaultAsync(cancellationToken);
        return lastId is null
            ? null
            : await ReadQuery(context).SingleAsync(
                analysis => analysis.Id == lastId.Value,
                cancellationToken);
    }

    private static IQueryable<AnalysisRunEntity> ReadQuery(GitHealthDbContext context)
    {
        return context.AnalysisRuns.AsNoTracking()
            .Include(analysis => analysis.Branches)
            .ThenInclude(branch => branch.Contributors);
    }

    private static async Task<ProjectEntity> FindProjectAsync(
        GitHealthDbContext context,
        Guid projectId,
        CancellationToken cancellationToken)
    {
        return await context.Projects.SingleOrDefaultAsync(
            project => project.Id == projectId,
            cancellationToken)
            ?? throw new KeyNotFoundException("Le projet demandé n’existe pas.");
    }

    private static async Task<AnalysisRunEntity> FindAnalysisAsync(
        GitHealthDbContext context,
        Guid analysisId,
        CancellationToken cancellationToken)
    {
        return await context.AnalysisRuns.Include(analysis => analysis.Project)
            .SingleOrDefaultAsync(analysis => analysis.Id == analysisId, cancellationToken)
            ?? throw new KeyNotFoundException("L’analyse demandée n’existe pas.");
    }
}
