using App.GitHealth.Api.Features.Common;
using App.GitHealth.Api.Persistence.Entities;
using App.GitHealth.Api.Persistence.Models;
using App.GitHealth.Api.Persistence.Repositories;

namespace App.GitHealth.Api.Features.Analyses;

internal sealed class AnalysisStatusService(
    IAnalysisRepository analyses,
    AnalysisQueue queue)
{
    public async Task<ApiOutcome<AnalysisStatusResponse>> GetAsync(
        Guid analysisId,
        CancellationToken cancellationToken)
    {
        var analysis = await analyses.GetAsync(analysisId, cancellationToken);
        return analysis is null
            ? ApiOutcome<AnalysisStatusResponse>.Failed(ApiProblems.NotFound(
                ApiErrorCodes.AnalysisNotFound,
                "The requested analysis does not exist."))
            : ApiOutcome<AnalysisStatusResponse>.Success(Map(analysis));
    }

    private AnalysisStatusResponse Map(AnalysisRunEntity analysis)
    {
        var isTracked = queue.TryGetProgress(analysis.Id, out var progress);
        var phase = isTracked ? progress!.Phase : MapPersistedPhase(analysis.Status);
        return new AnalysisStatusResponse
        {
            AnalysisId = analysis.Id,
            ProjectId = analysis.ProjectId,
            Status = analysis.Status.ToString(),
            Phase = phase.ToString(),
            StartedAtUtc = analysis.StartedAtUtc,
            CompletedAtUtc = analysis.CompletedAtUtc,
            FailureCode = analysis.FailureCode,
            FailureMessage = analysis.FailureMessage,
            Progress = isTracked ? AnalysisProgressMapper.ToResponse(progress!) : null,
        };
    }

    private static AnalysisPhase MapPersistedPhase(AnalysisRunStatus status) => status switch
    {
        AnalysisRunStatus.Completed => AnalysisPhase.Finished,
        AnalysisRunStatus.Failed => AnalysisPhase.Failed,
        AnalysisRunStatus.Cancelled => AnalysisPhase.Cancelled,
        _ => AnalysisPhase.Waiting,
    };
}
