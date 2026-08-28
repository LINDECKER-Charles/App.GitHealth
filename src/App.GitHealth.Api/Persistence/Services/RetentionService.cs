using App.GitHealth.Api.Persistence.Models;
using App.GitHealth.Api.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace App.GitHealth.Api.Persistence.Services;

internal sealed class RetentionService(
    IDbContextFactory<GitHealthDbContext> contextFactory,
    IOptions<PersistenceOptions> options) : IRetentionService
{
    public async Task<RetentionResult> ApplyAsync(
        DateTimeOffset nowUtc,
        CancellationToken cancellationToken)
    {
        UtcDate.Require(nowUtc, nameof(nowUtc));
        if (options.Value.RetentionDays is not { } retentionDays)
        {
            return new RetentionResult(false, 0);
        }

        var cutoff = nowUtc.AddDays(-retentionDays);
        return await SqliteWriteExecutor.ExecuteAsync(async () =>
        {
            await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
            var protectedIds = await ReadProtectedIdsAsync(context, cancellationToken);
            var deleted = await context.AnalysisRuns
                .Where(analysis => analysis.CompletedAtUtc < cutoff)
                .Where(analysis => !protectedIds.Contains(analysis.Id))
                .ExecuteDeleteAsync(cancellationToken);
            return new RetentionResult(true, deleted);
        });
    }

    private static Task<Guid[]> ReadProtectedIdsAsync(
        GitHealthDbContext context,
        CancellationToken cancellationToken)
    {
        return context.Projects.AsNoTracking()
            .Where(project => project.LastSuccessfulAnalysisId != null)
            .Select(project => project.LastSuccessfulAnalysisId!.Value)
            .ToArrayAsync(cancellationToken);
    }
}
