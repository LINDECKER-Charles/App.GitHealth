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

internal sealed record AnalysisWorkItem(Guid AnalysisId, Guid ProjectId);

internal sealed record AnalysisEnqueueResult(
    AnalysisEnqueueKind Kind,
    Guid? AnalysisId,
    bool IsDuplicate);

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

internal sealed record AnalysisProgressSnapshot
{
    public required Guid AnalysisId { get; init; }

    public required AnalysisPhase Phase { get; init; }

    public required DateTimeOffset UpdatedAtUtc { get; init; }

    public string? Message { get; init; }
}

internal sealed record AnalysisLaunchResponse
{
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
}
