using App.GitHealth.Api.Features.Common;
using App.GitHealth.Api.Persistence.Models;
using App.GitHealth.Api.Persistence.Repositories;
using App.GitHealth.Core.Common;
using App.GitHealth.Core.Projects;

namespace App.GitHealth.Api.Features.Projects;

/// <summary>
/// Moves a project inside the workspace. No read of the repository is needed:
/// the favourite flag and the group describe navigation only, never the Git state.
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
                "The requested project does not exist."));
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
