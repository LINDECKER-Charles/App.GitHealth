namespace App.GitHealth.Api.Persistence.Models;

/// <summary>
/// Outcome of removing one analysis run. A run in flight is refused rather than deleted:
/// the worker still holds it and would resurrect rows behind the delete.
/// </summary>
internal sealed record AnalysisDeletionResult(bool WasFound, bool WasRunning);

/// <summary>Slice of a project's history, optionally narrowed to a single baseline.</summary>
internal sealed record AnalysisHistoryRange(int Skip, int Take, string? Baseline = null);

internal sealed record AnalysisHistoryPage(
    IReadOnlyList<AnalysisHistoryRecord> Items,
    int TotalCount);

internal sealed record AnalysisHistoryRecord
{
    public required Guid AnalysisId { get; init; }

    public required AnalysisRunStatus Status { get; init; }

    public required DateTimeOffset StartedAtUtc { get; init; }

    public DateTimeOffset? CompletedAtUtc { get; init; }

    public DateTimeOffset? CapturedAtUtc { get; init; }

    public required string ReferenceName { get; init; }

    public string? ReferenceCommit { get; init; }

    public required string BranchNamespace { get; init; }

    public required int ActiveUntilDays { get; init; }

    public required int InactiveAfterDays { get; init; }

    public required string ExcludedPatternsJson { get; init; }

    public required string ProtectedPatternsJson { get; init; }

    public string? GitVersion { get; init; }

    public required int BranchCount { get; init; }

    public string? FailureCode { get; init; }

    public string? FailureMessage { get; init; }
}
