using App.GitHealth.Api.Persistence.Entities;
using App.GitHealth.Api.Persistence.Models;
using Microsoft.EntityFrameworkCore;

namespace App.GitHealth.Api.Persistence.Repositories;

internal sealed class AnalysisRepository(IDbContextFactory<GitHealthDbContext> contextFactory)
    : IAnalysisRepository
{
    public Task<Guid> StartAsync(
        AnalysisTarget target,
        DateTimeOffset startedAtUtc,
        CancellationToken cancellationToken)
    {
        return SqliteWriteExecutor.ExecuteAsync(async () =>
        {
            await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
            var project = await FindProjectAsync(context, target.ProjectId, cancellationToken);
            var analysis = AnalysisRunEntity.Start(project, target, startedAtUtc);
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
            PromoteLatest(analysis);
            analysis.Project.MarkAccessible(completion.CompletedAtUtc);
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

    /// <summary>
    /// Removes a run and hands its baseline back the previous capture. A run still in flight
    /// is reported rather than deleted: the worker would write its results behind the delete.
    /// </summary>
    public Task<AnalysisDeletionResult> DeleteAsync(
        Guid analysisId,
        CancellationToken cancellationToken)
    {
        return SqliteWriteExecutor.ExecuteAsync(async () =>
        {
            await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
            await using var transaction = await context.Database
                .BeginTransactionAsync(cancellationToken);
            var analysis = await context.AnalysisRuns
                .Include(run => run.Project)
                .ThenInclude(project => project.Baselines)
                .SingleOrDefaultAsync(run => run.Id == analysisId, cancellationToken);
            if (analysis is null || analysis.Status == AnalysisRunStatus.Running)
            {
                return new AnalysisDeletionResult(analysis is not null, analysis is not null);
            }

            await DemoteAsync(context, analysis, cancellationToken);
            context.AnalysisRuns.Remove(analysis);
            await context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return new AnalysisDeletionResult(true, false);
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
        return await ReadByIdAsync(context, lastId, cancellationToken);
    }

    public async Task<AnalysisRunEntity?> GetLastSuccessfulForBaselineAsync(
        AnalysisTarget target,
        CancellationToken cancellationToken)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var lastId = await context.ProjectBaselines.AsNoTracking()
            .Where(baseline => baseline.ProjectId == target.ProjectId)
            .Where(baseline => baseline.ReferenceName == target.ReferenceName)
            .Select(baseline => baseline.LastSuccessfulAnalysisId)
            .SingleOrDefaultAsync(cancellationToken);
        return await ReadByIdAsync(context, lastId, cancellationToken);
    }

    public async Task<BranchSnapshotEntity?> GetBranchAsync(
        Guid branchSnapshotId,
        CancellationToken cancellationToken)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        return await context.BranchSnapshots.AsNoTracking()
            .Include(branch => branch.AnalysisRun)
            .Include(branch => branch.Contributors)
            .SingleOrDefaultAsync(branch => branch.Id == branchSnapshotId, cancellationToken);
    }

    public async Task<AnalysisHistoryPage> GetHistoryAsync(
        Guid projectId,
        AnalysisHistoryRange range,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(range);
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var query = context.AnalysisRuns.AsNoTracking()
            .Where(analysis => analysis.ProjectId == projectId)
            .Where(analysis =>
                range.Baseline == null || analysis.ReferenceName == range.Baseline);
        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(analysis => analysis.StartedAtUtc)
            .ThenByDescending(analysis => analysis.Id)
            .Skip(range.Skip)
            .Take(range.Take)
            .Select(analysis => new AnalysisHistoryRecord
            {
                AnalysisId = analysis.Id,
                Status = analysis.Status,
                StartedAtUtc = analysis.StartedAtUtc,
                CompletedAtUtc = analysis.CompletedAtUtc,
                CapturedAtUtc = analysis.CapturedAtUtc,
                ReferenceName = analysis.ReferenceName,
                ReferenceCommit = analysis.ReferenceCommit,
                BranchNamespace = analysis.BranchNamespace,
                ActiveUntilDays = analysis.ActiveUntilDays,
                InactiveAfterDays = analysis.InactiveAfterDays,
                ExcludedPatternsJson = analysis.ExcludedPatternsJson,
                ProtectedPatternsJson = analysis.ProtectedPatternsJson,
                GitVersion = analysis.GitVersion,
                BranchCount = analysis.Branches.Count,
                FailureCode = analysis.FailureCode,
                FailureMessage = analysis.FailureMessage,
            })
            .ToListAsync(cancellationToken);
        return new AnalysisHistoryPage(items, totalCount);
    }

    /// <summary>
    /// Records the run against its own baseline, then rebuilds the project-wide pointer from
    /// the primary baseline. Runs of one project finish in any order; this does not care.
    /// </summary>
    private static void PromoteLatest(AnalysisRunEntity analysis)
    {
        var baseline = FindBaseline(analysis);
        if (baseline is not null)
        {
            baseline.LastSuccessfulAnalysisId = analysis.Id;
        }

        analysis.Project.PromoteLatestOfPrimaryBaseline();
    }

    private static async Task DemoteAsync(
        GitHealthDbContext context,
        AnalysisRunEntity analysis,
        CancellationToken cancellationToken)
    {
        var baseline = FindBaseline(analysis);
        if (baseline?.LastSuccessfulAnalysisId == analysis.Id)
        {
            baseline.LastSuccessfulAnalysisId = await FindPreviousAsync(
                context,
                analysis,
                cancellationToken);
        }

        analysis.Project.PromoteLatestOfPrimaryBaseline();
    }

    private static Task<Guid?> FindPreviousAsync(
        GitHealthDbContext context,
        AnalysisRunEntity analysis,
        CancellationToken cancellationToken)
    {
        return context.AnalysisRuns.AsNoTracking()
            .Where(run => run.ProjectId == analysis.ProjectId)
            .Where(run => run.ReferenceName == analysis.ReferenceName)
            .Where(run => run.Id != analysis.Id)
            .Where(run => run.Status == AnalysisRunStatus.Completed)
            .OrderByDescending(run => run.CapturedAtUtc)
            .ThenByDescending(run => run.StartedAtUtc)
            .ThenByDescending(run => run.Id)
            .Select(run => (Guid?)run.Id)
            .FirstOrDefaultAsync(cancellationToken);
    }

    private static ProjectBaselineEntity? FindBaseline(AnalysisRunEntity analysis) =>
        analysis.Project.Baselines.SingleOrDefault(baseline => string.Equals(
            baseline.ReferenceName,
            analysis.ReferenceName,
            StringComparison.Ordinal));

    /// <summary>
    /// The pointer carries no foreign key, so a stale one must read as "nothing captured"
    /// rather than throw. Deletion repairs it; this keeps a repaired-too-late DB usable.
    /// </summary>
    private static async Task<AnalysisRunEntity?> ReadByIdAsync(
        GitHealthDbContext context,
        Guid? analysisId,
        CancellationToken cancellationToken)
    {
        return analysisId is null
            ? null
            : await ReadQuery(context).SingleOrDefaultAsync(
                analysis => analysis.Id == analysisId.Value,
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
        return await context.Projects
            .Include(project => project.Baselines)
            .SingleOrDefaultAsync(project => project.Id == projectId, cancellationToken)
            ?? throw new KeyNotFoundException("The requested project does not exist.");
    }

    private static async Task<AnalysisRunEntity> FindAnalysisAsync(
        GitHealthDbContext context,
        Guid analysisId,
        CancellationToken cancellationToken)
    {
        return await context.AnalysisRuns
            .Include(analysis => analysis.Project)
            .ThenInclude(project => project.Baselines)
            .SingleOrDefaultAsync(analysis => analysis.Id == analysisId, cancellationToken)
            ?? throw new KeyNotFoundException("The requested analysis does not exist.");
    }
}
