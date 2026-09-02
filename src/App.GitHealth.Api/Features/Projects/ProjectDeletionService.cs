using App.GitHealth.Api.Features.Analyses;
using App.GitHealth.Api.Features.Common;
using App.GitHealth.Api.Persistence.Repositories;

namespace App.GitHealth.Api.Features.Projects;

/// <summary>
/// Removes a project and everything GitHealth measured about it. The Git repository on disk
/// is never touched — only the observations disappear.
/// </summary>
internal sealed class ProjectDeletionService(IProjectRepository projects, AnalysisQueue queue)
{
    public async Task<ApiOutcome<bool>> DeleteAsync(
        Guid projectId,
        CancellationToken cancellationToken)
    {
        await using var reservation = await queue.TryReserveProjectAsync(
            projectId,
            cancellationToken);
        if (reservation is null)
        {
            return ApiOutcome<bool>.Failed(ApiProblems.Conflict(
                ApiErrorCodes.ProjectBusy,
                "The project is busy with an analysis or a relocation in progress."));
        }

        var deleted = await projects.DeleteAsync(projectId, cancellationToken);
        return deleted
            ? ApiOutcome<bool>.Success(true)
            : ApiOutcome<bool>.Failed(ApiProblems.NotFound(
                ApiErrorCodes.ProjectNotFound,
                "The requested project does not exist."));
    }
}
