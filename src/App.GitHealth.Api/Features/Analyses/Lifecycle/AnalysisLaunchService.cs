using App.GitHealth.Api.Features.Common;
using App.GitHealth.Api.Persistence.Entities;
using App.GitHealth.Api.Persistence.Models;
using App.GitHealth.Api.Persistence.Repositories;

namespace App.GitHealth.Api.Features.Analyses.Lifecycle;

/// <summary>
/// Turns one "run an analysis" into one run per declared baseline. Each run measures the
/// repository against a single baseline, so three baselines mean three independent runs —
/// each with its own timeout, its own progress and its own failure.
/// </summary>
internal sealed class AnalysisLaunchService(IProjectRepository projects, AnalysisQueue queue)
{
    public async Task<ApiOutcome<AnalysisLaunchResponse>> LaunchAsync(
        Guid projectId,
        AnalysisLaunchQueryParameters query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        var project = await projects.GetAsync(projectId, cancellationToken);
        if (project is null)
        {
            return ApiOutcome<AnalysisLaunchResponse>.Failed(ProjectNotFound());
        }

        var baselines = SelectBaselines(project, Requested(query));
        if (!baselines.IsSuccess)
        {
            return ApiOutcome<AnalysisLaunchResponse>.Failed(baselines.Failure!);
        }

        var results = await EnqueueAllAsync(projectId, baselines.Value!, cancellationToken);
        return Map(results);
    }

    private async Task<IReadOnlyList<AnalysisEnqueueResult>> EnqueueAllAsync(
        Guid projectId,
        IReadOnlyList<string> baselines,
        CancellationToken cancellationToken)
    {
        var results = new List<AnalysisEnqueueResult>(baselines.Count);
        foreach (var baseline in baselines)
        {
            var target = new AnalysisTarget(projectId, baseline);
            results.Add(await queue.EnqueueAsync(target, cancellationToken));
        }

        return results;
    }

    /// <summary>
    /// A launch succeeds as soon as one baseline was taken: a queue that fills up halfway
    /// through must still report the runs it did accept, not throw all of them away.
    /// </summary>
    private static ApiOutcome<AnalysisLaunchResponse> Map(
        IReadOnlyList<AnalysisEnqueueResult> results)
    {
        // A saturated queue still records its run as failed, so a non-null identifier is
        // not proof the work was taken: only the kind says whether it will actually run.
        var accepted = results.Where(IsRunning).ToArray();
        if (accepted.Length == 0)
        {
            return ApiOutcome<AnalysisLaunchResponse>.Failed(
                Rejection(results[0].Kind));
        }

        var items = accepted
            .Select(result => new AnalysisLaunchItem(
                result.AnalysisId!.Value,
                result.ReferenceName,
                StatusUrl(result.AnalysisId.Value),
                result.IsDuplicate))
            .ToArray();
        return ApiOutcome<AnalysisLaunchResponse>.Success(new AnalysisLaunchResponse
        {
            Analyses = items,
            AnalysisId = items[0].AnalysisId,
            StatusUrl = items[0].StatusUrl,
            IsDuplicate = items.All(item => item.IsDuplicate),
        });
    }

    private static ApiOutcome<IReadOnlyList<string>> SelectBaselines(
        ProjectEntity project,
        string? requested)
    {
        var declared = project.Baselines
            .OrderBy(baseline => baseline.Position)
            .Select(baseline => baseline.ReferenceName)
            .ToArray();
        if (declared.Length == 0)
        {
            return ApiOutcome<IReadOnlyList<string>>.Failed(ApiProblems.BadRequest(
                ApiErrorCodes.InvalidReference,
                "The project declares no baseline to compare against."));
        }

        if (requested is null)
        {
            return ApiOutcome<IReadOnlyList<string>>.Success(declared);
        }

        return declared.Contains(requested, StringComparer.Ordinal)
            ? ApiOutcome<IReadOnlyList<string>>.Success([requested])
            : ApiOutcome<IReadOnlyList<string>>.Failed(ApiProblems.BadRequest(
                ApiErrorCodes.InvalidReference,
                "The project does not declare this baseline."));
    }

    private static bool IsRunning(AnalysisEnqueueResult result) =>
        result.AnalysisId is not null
        && result.Kind is AnalysisEnqueueKind.Accepted or AnalysisEnqueueKind.Duplicate;

    private static ApiFailure Rejection(AnalysisEnqueueKind kind) => kind switch
    {
        AnalysisEnqueueKind.ProjectNotFound => ProjectNotFound(),
        AnalysisEnqueueKind.ProjectBusy => ApiProblems.Conflict(
            ApiErrorCodes.ProjectBusy,
            "The project is reserved by a relocation in progress."),
        _ => ApiProblems.Unavailable(
            ApiErrorCodes.QueueFull,
            "The analysis queue is full."),
    };

    private static ApiFailure ProjectNotFound() => ApiProblems.NotFound(
        ApiErrorCodes.ProjectNotFound,
        "The requested project does not exist.");

    private static string? Requested(AnalysisLaunchQueryParameters query) =>
        string.IsNullOrWhiteSpace(query.Baseline) ? null : query.Baseline.Trim();

    private static string StatusUrl(Guid analysisId) => $"/api/analyses/{analysisId}";
}
