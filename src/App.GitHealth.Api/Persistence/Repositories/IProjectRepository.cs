using App.GitHealth.Api.Persistence.Entities;
using App.GitHealth.Api.Persistence.Models;
using App.GitHealth.Core.Projects;

namespace App.GitHealth.Api.Persistence.Repositories;

internal interface IProjectRepository
{
    Task<ProjectEntity> AddAsync(
        Project project,
        DateTimeOffset createdAtUtc,
        CancellationToken cancellationToken);

    Task<ProjectEntity?> GetAsync(Guid projectId, CancellationToken cancellationToken);

    Task<string?> GetLastSuccessfulReferenceCommitAsync(
        Guid projectId,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<ProjectEntity>> ListAsync(CancellationToken cancellationToken);

    Task<bool> DeleteAsync(Guid projectId, CancellationToken cancellationToken);

    Task RelocateAsync(ProjectRelocation relocation, CancellationToken cancellationToken);

    Task UpdateSettingsAsync(ProjectSettingsUpdate update, CancellationToken cancellationToken);

    Task UpdateOrganizationAsync(
        ProjectOrganizationUpdate update,
        CancellationToken cancellationToken);

    Task MarkUnavailableAsync(
        Guid projectId,
        DateTimeOffset changedAtUtc,
        CancellationToken cancellationToken);
}
