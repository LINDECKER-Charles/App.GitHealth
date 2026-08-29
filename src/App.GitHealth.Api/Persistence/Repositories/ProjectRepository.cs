using App.GitHealth.Api.Persistence.Entities;
using App.GitHealth.Api.Persistence.Models;
using App.GitHealth.Core.Projects;
using Microsoft.EntityFrameworkCore;

namespace App.GitHealth.Api.Persistence.Repositories;

internal sealed class ProjectRepository(IDbContextFactory<GitHealthDbContext> contextFactory)
    : IProjectRepository
{
    public Task<ProjectEntity> AddAsync(
        Project project,
        DateTimeOffset createdAtUtc,
        CancellationToken cancellationToken)
    {
        return SqliteWriteExecutor.ExecuteAsync(async () =>
        {
            await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
            var entity = ProjectEntity.Create(project, createdAtUtc);
            context.Projects.Add(entity);
            await context.SaveChangesAsync(cancellationToken);
            return entity;
        });
    }

    public async Task<ProjectEntity?> GetAsync(
        Guid projectId,
        CancellationToken cancellationToken)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        return await context.Projects.AsNoTracking()
            .SingleOrDefaultAsync(project => project.Id == projectId, cancellationToken);
    }

    public async Task<IReadOnlyList<ProjectEntity>> ListAsync(
        CancellationToken cancellationToken)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        return await context.Projects.AsNoTracking()
            .OrderByDescending(project => project.UpdatedAtUtc)
            .ThenBy(project => project.Id)
            .ToListAsync(cancellationToken);
    }

    public async Task<string?> GetLastSuccessfulReferenceCommitAsync(
        Guid projectId,
        CancellationToken cancellationToken)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var analysisId = await context.Projects.AsNoTracking()
            .Where(project => project.Id == projectId)
            .Select(project => project.LastSuccessfulAnalysisId)
            .SingleOrDefaultAsync(cancellationToken);
        return analysisId is null
            ? null
            : await context.AnalysisRuns.AsNoTracking()
                .Where(analysis => analysis.Id == analysisId.Value)
                .Select(analysis => analysis.ReferenceCommit)
                .SingleOrDefaultAsync(cancellationToken);
    }

    public Task RelocateAsync(
        ProjectRelocation relocation,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(relocation);
        return UpdateAsync(
            relocation.ProjectId,
            project => project.Relocate(relocation.RepositoryPath, relocation.ChangedAtUtc),
            cancellationToken);
    }

    public Task MarkUnavailableAsync(
        Guid projectId,
        DateTimeOffset changedAtUtc,
        CancellationToken cancellationToken)
    {
        return UpdateAsync(
            projectId,
            project => project.MarkUnavailable(changedAtUtc),
            cancellationToken);
    }

    public Task UpdateSettingsAsync(
        ProjectSettingsUpdate update,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(update);
        return UpdateAsync(
            update.ProjectId,
            project => project.UpdateSettings(update.Settings, update.ChangedAtUtc),
            cancellationToken);
    }

    private Task<bool> UpdateAsync(
        Guid projectId,
        Action<ProjectEntity> update,
        CancellationToken cancellationToken)
    {
        return SqliteWriteExecutor.ExecuteAsync(async () =>
        {
            await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
            var entity = await FindRequiredAsync(context, projectId, cancellationToken);
            update(entity);
            await context.SaveChangesAsync(cancellationToken);
            return true;
        });
    }

    private static async Task<ProjectEntity> FindRequiredAsync(
        GitHealthDbContext context,
        Guid projectId,
        CancellationToken cancellationToken)
    {
        return await context.Projects.SingleOrDefaultAsync(
            project => project.Id == projectId,
            cancellationToken)
            ?? throw new KeyNotFoundException("Le projet demandé n’existe pas.");
    }
}
