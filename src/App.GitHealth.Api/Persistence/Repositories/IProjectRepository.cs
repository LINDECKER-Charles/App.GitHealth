using App.GitHealth.Api.Persistence.Entities;
using App.GitHealth.Api.Persistence.Models;
using App.GitHealth.Api.Persistence.Models.Assistant;
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

    /// <summary>
    /// Every project whose schedule is switched on, whatever it says. Read once a tick, so it
    /// carries no baseline: the launch reads the project again on its own scope.
    /// </summary>
    Task<IReadOnlyList<ProjectEntity>> ListScheduledAsync(CancellationToken cancellationToken);

    Task<bool> DeleteAsync(Guid projectId, CancellationToken cancellationToken);

    Task RelocateAsync(ProjectRelocation relocation, CancellationToken cancellationToken);

    Task UpdateSettingsAsync(ProjectSettingsUpdate update, CancellationToken cancellationToken);

    Task UpdateOrganizationAsync(
        ProjectOrganizationUpdate update,
        CancellationToken cancellationToken);

    Task UpdateScheduleAsync(ProjectScheduleUpdate update, CancellationToken cancellationToken);

    /// <summary>Records a firing, which closes the window the scheduler has just acted on.</summary>
    Task MarkScheduleRunAsync(
        Guid projectId,
        DateTimeOffset ranAtUtc,
        CancellationToken cancellationToken);

    Task MarkUnavailableAsync(
        Guid projectId,
        DateTimeOffset changedAtUtc,
        CancellationToken cancellationToken);

    Task SetAssistantConsentAsync(
        AssistantConsentUpdate update,
        CancellationToken cancellationToken);
}
