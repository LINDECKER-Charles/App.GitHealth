using App.GitHealth.Api.Persistence.Models;
using Microsoft.AspNetCore.Mvc;

namespace App.GitHealth.Api.Features.Analyses;

internal enum AnalysisPhase
{
    Waiting,
    Topology,
    Enrichment,
    Persistence,
    Finished,
    Failed,
    Cancelled,
}

internal enum AnalysisEnqueueKind
{
    Accepted,
    Duplicate,
    QueueFull,
    ProjectNotFound,
    ProjectBusy,
}

internal sealed record AnalysisWorkItem(Guid AnalysisId, AnalysisTarget Target)
{
    public Guid ProjectId => Target.ProjectId;
}

internal sealed record AnalysisEnqueueResult(
    AnalysisEnqueueKind Kind,
    Guid? AnalysisId,
    bool IsDuplicate)
{
    /// <summary>Baseline this outcome concerns, so a fan-out can report each one by name.</summary>
    public string ReferenceName { get; init; } = string.Empty;
}

internal sealed class ProjectOperationReservation(
    AnalysisQueue queue,
    Guid projectId) : IAsyncDisposable
{
    private int _isDisposed;

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _isDisposed, 1) == 0)
        {
            await queue.ReleaseReservationAsync(projectId);
        }
    }
}

/// <summary>
/// Launching stays a bodyless POST, as it has always been: the baseline travels in the query
/// string like every other selector in this API.
/// </summary>
internal sealed record AnalysisLaunchQueryParameters
{
    /// <summary>Single baseline to measure. Absent means every baseline of the project.</summary>
    [FromQuery(Name = "baseline")]
    public string? Baseline { get; init; }
}

internal sealed record AnalysisLaunchItem(
    Guid AnalysisId,
    string ReferenceName,
    string StatusUrl,
    bool IsDuplicate);

internal sealed record AnalysisLaunchResponse
{
    /// <summary>One entry per baseline measured by this launch, in project order.</summary>
    public required IReadOnlyList<AnalysisLaunchItem> Analyses { get; init; }

    /// <summary>Run of the primary baseline, which a single-baseline reader follows.</summary>
    public required Guid AnalysisId { get; init; }

    public required string StatusUrl { get; init; }

    public required bool IsDuplicate { get; init; }
}

internal sealed record AnalysisStatusResponse
{
    public required Guid AnalysisId { get; init; }

    public required Guid ProjectId { get; init; }

    public required string Status { get; init; }

    public required string Phase { get; init; }

    public required DateTimeOffset StartedAtUtc { get; init; }

    public DateTimeOffset? CompletedAtUtc { get; init; }

    public string? FailureCode { get; init; }

    public string? FailureMessage { get; init; }

    /// <summary>What the run is doing right now; absent once it is no longer running.</summary>
    public AnalysisProgressResponse? Progress { get; init; }
}

internal sealed record AnalysisProgressResponse
{
    /// <summary>Every reference of the run, in read order, each with what is known of it.</summary>
    public required IReadOnlyList<AnalysisReferenceResponse> References { get; init; }

    /// <summary>Tail of the Git commands run, oldest first.</summary>
    public required IReadOnlyList<AnalysisCommandResponse> Commands { get; init; }

    /// <summary>Commands run since the start, including those no longer in the tail.</summary>
    public required int CommandCount { get; init; }
}

internal sealed record AnalysisReferenceResponse
{
    public required string ReferenceName { get; init; }

    public required string CommitId { get; init; }

    public required string State { get; init; }

    public DateTimeOffset? LastActivityAtUtc { get; init; }

    public string? TipAuthor { get; init; }

    public string? MergeBaseCommit { get; init; }

    public int? AheadCount { get; init; }

    public int? BehindCount { get; init; }

    public string? Topology { get; init; }

    public string? TopContributor { get; init; }

    public int? ContributorCount { get; init; }
}

internal sealed record AnalysisCommandResponse
{
    public required int Sequence { get; init; }

    public required string CommandLine { get; init; }

    public required int DurationMs { get; init; }

    public required int ExitCode { get; init; }

    public string? Output { get; init; }
}
