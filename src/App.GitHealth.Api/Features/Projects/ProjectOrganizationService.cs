using App.GitHealth.Api.Features.Common;
using App.GitHealth.Api.Persistence.Models;
using App.GitHealth.Api.Persistence.Repositories;
using App.GitHealth.Core.Common;
using App.GitHealth.Core.Projects;

namespace App.GitHealth.Api.Features.Projects;

/// <summary>
/// Range un projet dans l'espace de travail. Aucune lecture du dépôt n'est nécessaire :
/// le favori et le groupe ne décrivent que la navigation, jamais l'état Git.
/// </summary>
internal sealed class ProjectOrganizationService(IProjectRepository projects, IClock clock)
{
    public async Task<ApiOutcome<ProjectResponse>> UpdateAsync(
        Guid projectId,
        ProjectOrganizationRequest request,
        CancellationToken cancellationToken)
    {
        var stored = await projects.GetAsync(projectId, cancellationToken);
        if (stored is null)
        {
            return ApiOutcome<ProjectResponse>.Failed(ApiProblems.NotFound(
                ApiErrorCodes.ProjectNotFound,
                "Le projet demandé n’existe pas."));
        }

        var organization = Build(request);
        if (!organization.IsSuccess)
        {
            return ApiOutcome<ProjectResponse>.Failed(organization.Failure!);
        }

        var update = new ProjectOrganizationUpdate(projectId, organization.Value!, clock.UtcNow);
        await projects.UpdateOrganizationAsync(update, cancellationToken);
        var updated = await projects.GetAsync(projectId, cancellationToken);
        return ApiOutcome<ProjectResponse>.Success(ProjectResponseMapper.Map(updated!));
    }

    private static ApiOutcome<ProjectOrganization> Build(ProjectOrganizationRequest request)
    {
        try
        {
            return ApiOutcome<ProjectOrganization>.Success(new ProjectOrganization
            {
                IsFavorite = request.IsFavorite,
                GroupName = request.GroupName,
            });
        }
        catch (ArgumentException exception)
        {
            return ApiOutcome<ProjectOrganization>.Failed(ApiProblems.BadRequest(
                ApiErrorCodes.InvalidRequest,
                exception.Message));
        }
    }
}
