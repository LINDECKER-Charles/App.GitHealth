using App.GitHealth.Api.Features.Common;
using App.GitHealth.Api.Persistence.Repositories;

namespace App.GitHealth.Api.Features.Snapshots;

internal sealed class SnapshotService(
    IAnalysisRepository analyses,
    SnapshotMapper mapper)
{
    public async Task<ApiOutcome<SnapshotPageResponse>> GetPageAsync(
        Guid projectId,
        SnapshotQueryParameters query,
        CancellationToken cancellationToken)
    {
        var analysis = await analyses.GetLastSuccessfulAsync(projectId, cancellationToken);
        if (analysis is null)
        {
            return ApiOutcome<SnapshotPageResponse>.Failed(NoSuccessfulResult());
        }

        var page = SnapshotPaginator.Page(analysis, query);
        if (!page.IsSuccess)
        {
            return ApiOutcome<SnapshotPageResponse>.Failed(page.Failure!);
        }

        var response = new SnapshotPageResponse
        {
            AnalysisId = analysis.Id,
            CapturedAtUtc = analysis.CapturedAtUtc!.Value,
            ReferenceName = analysis.ReferenceName,
            Items = page.Value!.Branches.Select(branch => mapper.Map(analysis, branch)).ToArray(),
            NextCursor = page.Value.NextCursor,
        };
        return ApiOutcome<SnapshotPageResponse>.Success(response);
    }

    public async Task<ApiOutcome<SnapshotDetailResponse>> GetDetailAsync(
        Guid snapshotId,
        CancellationToken cancellationToken)
    {
        var branch = await analyses.GetBranchAsync(snapshotId, cancellationToken);
        return branch is null
            ? ApiOutcome<SnapshotDetailResponse>.Failed(ApiProblems.NotFound(
                ApiErrorCodes.SnapshotNotFound,
                "Le snapshot demandé n’existe pas."))
            : ApiOutcome<SnapshotDetailResponse>.Success(mapper.MapDetail(branch));
    }

    private static ApiFailure NoSuccessfulResult() => ApiProblems.NotFound(
        ApiErrorCodes.AnalysisNotAvailable,
        "Aucune analyse réussie n’est disponible pour ce projet.");
}
