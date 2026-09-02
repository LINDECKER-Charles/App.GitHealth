using App.GitHealth.Api.Features.Common;
using App.GitHealth.Api.Persistence.Repositories;

namespace App.GitHealth.Api.Features.Analyses.Lifecycle;

/// <summary>
/// Removes one capture from a project's history. The repository is never touched: only the
/// measurements GitHealth took of it disappear.
/// </summary>
internal sealed class AnalysisDeletionService(
    IAnalysisRepository analyses,
    AnalysisQueue queue)
{
    public async Task<ApiOutcome<bool>> DeleteAsync(
        Guid analysisId,
        CancellationToken cancellationToken)
    {
        var analysis = await analyses.GetAsync(analysisId, cancellationToken);
        if (analysis is null)
        {
            return ApiOutcome<bool>.Failed(AnalysisNotFound());
        }

        await using var reservation = await queue.TryReserveProjectAsync(
            analysis.ProjectId,
            cancellationToken);
        return reservation is null
            ? ApiOutcome<bool>.Failed(ApiProblems.Conflict(
                ApiErrorCodes.ProjectBusy,
                "The project is busy with an analysis or a relocation in progress."))
            : await DeleteReservedAsync(analysisId, cancellationToken);
    }

    private async Task<ApiOutcome<bool>> DeleteReservedAsync(
        Guid analysisId,
        CancellationToken cancellationToken)
    {
        var result = await analyses.DeleteAsync(analysisId, cancellationToken);
        if (!result.WasFound)
        {
            return ApiOutcome<bool>.Failed(AnalysisNotFound());
        }

        return result.WasRunning
            ? ApiOutcome<bool>.Failed(ApiProblems.Conflict(
                ApiErrorCodes.AnalysisRunning,
                "A running analysis cannot be deleted. Wait for it to finish."))
            : ApiOutcome<bool>.Success(true);
    }

    private static ApiFailure AnalysisNotFound() => ApiProblems.NotFound(
        ApiErrorCodes.AnalysisNotFound,
        "The requested analysis does not exist.");
}
