using App.GitHealth.Core.Branches;

namespace App.GitHealth.Core.Analysis;

/// <summary>
/// A reference has been placed against the baseline: its distance is known.
/// </summary>
public sealed record ScanReferenceMeasured : RepositoryScanEvent
{
    public required string ReferenceName { get; init; }

    public required BranchDivergence Divergence { get; init; }

    /// <summary>
    /// Commit the two histories share, when the scan established one. Null when the
    /// reference has no common ancestor with the baseline.
    /// </summary>
    public string? MergeBaseCommit { get; init; }
}
