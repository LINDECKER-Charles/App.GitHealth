using App.GitHealth.Api.Features.Common;
using App.GitHealth.Api.Persistence.Entities;
using App.GitHealth.Api.Persistence.Models;
using App.GitHealth.Api.Persistence.Repositories;
using App.GitHealth.Core.Projects;

namespace App.GitHealth.Api.Features.Snapshots;

internal sealed class SnapshotService(
    IAnalysisRepository analyses,
    IProjectRepository projects,
    SnapshotMapper mapper)
{
    public async Task<ApiOutcome<SnapshotPageResponse>> GetPageAsync(
        Guid projectId,
        SnapshotQueryParameters query,
        CancellationToken cancellationToken)
    {
        var source = await LoadLatestAsync(projectId, cancellationToken);
        return source.IsSuccess
            ? BuildPage(source.Value!, query)
            : ApiOutcome<SnapshotPageResponse>.Failed(source.Failure!);
    }

    public async Task<ApiOutcome<SnapshotPageResponse>> GetAnalysisPageAsync(
        Guid analysisId,
        SnapshotQueryParameters query,
        CancellationToken cancellationToken)
    {
        var analysis = await analyses.GetAsync(analysisId, cancellationToken);
        if (!HasResults(analysis))
        {
            return ApiOutcome<SnapshotPageResponse>.Failed(AnalysisResultsNotAvailable());
        }

        var source = CreateCapturedSource(analysis!);
        return BuildPage(source, query);
    }

    public async Task<ApiOutcome<SnapshotSelectionData>> GetSelectionAsync(
        Guid projectId,
        SnapshotFilterParameters query,
        CancellationToken cancellationToken)
    {
        var source = await LoadLatestAsync(projectId, cancellationToken);
        if (!source.IsSuccess)
        {
            return ApiOutcome<SnapshotSelectionData>.Failed(source.Failure!);
        }

        var selected = SnapshotPaginator.Select(source.Value!.Branches, query);
        return selected.IsSuccess
            ? ApiOutcome<SnapshotSelectionData>.Success(
                new SnapshotSelectionData(
                    source.Value.Analysis,
                    selected.Value!,
                    source.Value.Policy))
            : ApiOutcome<SnapshotSelectionData>.Failed(selected.Failure!);
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
            : ApiOutcome<SnapshotDetailResponse>.Success(SnapshotMapper.MapDetail(branch));
    }

    private async Task<ApiOutcome<SnapshotSelectionData>> LoadLatestAsync(
        Guid projectId,
        CancellationToken cancellationToken)
    {
        var project = await projects.GetAsync(projectId, cancellationToken);
        if (project is null)
        {
            return ApiOutcome<SnapshotSelectionData>.Failed(ProjectNotFound());
        }

        var analysis = await analyses.GetLastSuccessfulAsync(projectId, cancellationToken);
        if (analysis is null)
        {
            return ApiOutcome<SnapshotSelectionData>.Failed(NoSuccessfulResult());
        }

        return ApiOutcome<SnapshotSelectionData>.Success(
            CreateCurrentSource(analysis, project.ToDomain().Settings));
    }

    private SnapshotSelectionData CreateCurrentSource(
        AnalysisRunEntity analysis,
        ProjectSettings settings)
    {
        var classified = analysis.Branches
            .Select(branch => mapper.Classify(branch, settings.Thresholds, settings.Policy))
            .ToArray();
        var policy = new SnapshotPolicyResponse
        {
            ActiveUntilDays = settings.Thresholds.ActiveUntilDays,
            InactiveAfterDays = settings.Thresholds.InactiveAfterDays,
            ExcludedPatterns = settings.Policy.ExcludedPatterns,
            ProtectedPatterns = settings.Policy.ProtectedPatterns,
        };
        return new SnapshotSelectionData(analysis, classified, policy);
    }

    private static SnapshotSelectionData CreateCapturedSource(AnalysisRunEntity analysis)
    {
        var classified = analysis.Branches
            .Select(branch => SnapshotMapper.ClassifyCaptured(analysis, branch))
            .ToArray();
        return new SnapshotSelectionData(analysis, classified, SnapshotMapper.MapPolicy(analysis));
    }

    private static ApiOutcome<SnapshotPageResponse> BuildPage(
        SnapshotSelectionData source,
        SnapshotQueryParameters query)
    {
        var page = SnapshotPaginator.Page(source.Analysis.Id, source.Branches, query);
        if (!page.IsSuccess)
        {
            return ApiOutcome<SnapshotPageResponse>.Failed(page.Failure!);
        }

        var response = MapPage(source, page.Value!);
        return ApiOutcome<SnapshotPageResponse>.Success(response);
    }

    private static SnapshotPageResponse MapPage(
        SnapshotSelectionData source,
        SnapshotPageData page) => new()
        {
            AnalysisId = source.Analysis.Id,
            CapturedAtUtc = source.Analysis.CapturedAtUtc!.Value,
            ReferenceName = source.Analysis.ReferenceName,
            Policy = source.Policy,
            Items = page.Branches.Select(SnapshotMapper.Map).ToArray(),
            NextCursor = page.NextCursor,
        };

    private static bool HasResults(AnalysisRunEntity? analysis) =>
        analysis?.Status == AnalysisRunStatus.Completed
        && analysis.CapturedAtUtc.HasValue
        && analysis.ReferenceCommit is not null;

    private static ApiFailure ProjectNotFound() => ApiProblems.NotFound(
        ApiErrorCodes.ProjectNotFound,
        "Le projet demandé n’existe pas.");

    private static ApiFailure NoSuccessfulResult() => ApiProblems.NotFound(
        ApiErrorCodes.AnalysisNotAvailable,
        "Aucune analyse réussie n’est disponible pour ce projet.");

    private static ApiFailure AnalysisResultsNotAvailable() => ApiProblems.NotFound(
        ApiErrorCodes.AnalysisNotAvailable,
        "Les résultats de cette analyse ne sont pas disponibles.");
}
