using App.GitHealth.Api.Features.Common;
using App.GitHealth.Api.Features.Projects;
using App.GitHealth.Api.Features.Snapshots;
using App.GitHealth.Api.Persistence.Entities;
using App.GitHealth.Api.Persistence.Models;
using App.GitHealth.Api.Persistence.Repositories;
using App.GitHealth.Core.Branches;
using App.GitHealth.Core.Common;
using App.GitHealth.Core.Projects;

namespace App.GitHealth.Api.Features.Policies;

internal sealed class PolicyService(
    IProjectRepository projects,
    IAnalysisRepository analyses,
    IClock clock)
{
    public async Task<ApiOutcome<ProjectResponse>> UpdateAsync(
        Guid projectId,
        PolicyUpdateRequest request,
        CancellationToken cancellationToken)
    {
        var project = await projects.GetAsync(projectId, cancellationToken);
        if (project is null)
        {
            return ApiOutcome<ProjectResponse>.Failed(ProjectNotFound());
        }

        var settings = BuildSettings(project, request);
        if (!settings.IsSuccess)
        {
            return ApiOutcome<ProjectResponse>.Failed(settings.Failure!);
        }

        var update = new ProjectSettingsUpdate(projectId, settings.Value!, clock.UtcNow);
        await projects.UpdateSettingsAsync(update, cancellationToken);
        var updated = await projects.GetAsync(projectId, cancellationToken);
        return ApiOutcome<ProjectResponse>.Success(ProjectResponseMapper.Map(updated!));
    }

    public async Task<ApiOutcome<PolicyPreviewResponse>> PreviewAsync(
        Guid projectId,
        PolicyUpdateRequest request,
        CancellationToken cancellationToken)
    {
        var project = await projects.GetAsync(projectId, cancellationToken);
        if (project is null)
        {
            return ApiOutcome<PolicyPreviewResponse>.Failed(ProjectNotFound());
        }

        var settings = BuildSettings(project, request);
        if (!settings.IsSuccess)
        {
            return ApiOutcome<PolicyPreviewResponse>.Failed(settings.Failure!);
        }

        var analysis = await analyses.GetLastSuccessfulAsync(projectId, cancellationToken);
        return analysis is null
            ? ApiOutcome<PolicyPreviewResponse>.Failed(NoSuccessfulResult())
            : ApiOutcome<PolicyPreviewResponse>.Success(
                MapPreview(analysis, settings.Value!));
    }

    private PolicyPreviewResponse MapPreview(
        AnalysisRunEntity analysis,
        ProjectSettings settings)
    {
        var classifier = new BranchClassifier(clock);
        var matches = analysis.Branches
            .Select(branch => Classify(branch, settings, classifier))
            .OrderBy(item => item.Branch.ReferenceName, StringComparer.Ordinal)
            .Select(item => new PolicyPreviewMatchResponse
            {
                ReferenceName = item.Branch.ReferenceName,
                IsExcluded = item.Comparison.IsExcluded,
                IsProtected = item.Comparison.IsProtected,
                Reason = item.Comparison.Reason,
            })
            .ToArray();
        return new PolicyPreviewResponse { Matches = matches };
    }

    private static ClassifiedSnapshot Classify(
        BranchSnapshotEntity branch,
        ProjectSettings settings,
        BranchClassifier classifier) => new(
            branch,
            classifier.Classify(
                SnapshotMapper.MapFacts(branch),
                settings.Thresholds,
                settings.Policy));

    private static ApiOutcome<ProjectSettings> BuildSettings(
        ProjectEntity project,
        PolicyUpdateRequest request)
    {
        try
        {
            // `with` rather than a fresh record: a policy edit must never touch the
            // baselines or the branch scope, and only copying what changes guarantees it.
            var current = project.ToDomain().Settings;
            return ApiOutcome<ProjectSettings>.Success(current with
            {
                Thresholds = ActivityThresholds.Create(
                    request.ActiveUntilDays,
                    request.InactiveAfterDays),
                Policy = BranchPolicy.Create(
                    request.ExcludedPatterns,
                    request.ProtectedPatterns),
            });
        }
        catch (ArgumentException exception)
        {
            return ApiOutcome<ProjectSettings>.Failed(ApiProblems.BadRequest(
                ApiErrorCodes.InvalidRequest,
                exception.Message));
        }
    }

    private static ApiFailure ProjectNotFound() => ApiProblems.NotFound(
        ApiErrorCodes.ProjectNotFound,
        "The requested project does not exist.");

    private static ApiFailure NoSuccessfulResult() => ApiProblems.NotFound(
        ApiErrorCodes.AnalysisNotAvailable,
        "No successful analysis is available for this project.");
}
