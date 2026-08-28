using App.GitHealth.Api.Persistence.Entities;
using Microsoft.AspNetCore.Mvc;

namespace App.GitHealth.Api.Features.Snapshots;

internal record SnapshotFilterParameters
{
    [FromQuery(Name = "search")]
    public string? Search { get; init; }

    [FromQuery(Name = "relationship")]
    public string? Relationship { get; init; }

    [FromQuery(Name = "topology")]
    public string? Topology { get; init; }

    [FromQuery(Name = "activity")]
    public string? Activity { get; init; }

    [FromQuery(Name = "recommendation")]
    public string? Recommendation { get; init; }

    [FromQuery(Name = "isProtected")]
    public bool? IsProtected { get; init; }

    [FromQuery(Name = "isExcluded")]
    public bool? IsExcluded { get; init; }

    [FromQuery(Name = "sort")]
    public string? Sort { get; init; }

    [FromQuery(Name = "direction")]
    public string? Direction { get; init; }
}

internal sealed record SnapshotQueryParameters : SnapshotFilterParameters
{
    [FromQuery(Name = "cursor")]
    public string? Cursor { get; init; }

    [FromQuery(Name = "pageSize")]
    public int? PageSize { get; init; }
}

internal sealed record SnapshotCursorData
{
    public required Guid AnalysisId { get; init; }

    public required string Sort { get; init; }

    public required string Direction { get; init; }

    public string? Search { get; init; }

    public string? Relationship { get; init; }

    public string? Topology { get; init; }

    public string? Activity { get; init; }

    public string? Recommendation { get; init; }

    public bool? IsProtected { get; init; }

    public bool? IsExcluded { get; init; }

    public required string SortValue { get; init; }

    public required string ReferenceName { get; init; }

    public required Guid SnapshotId { get; init; }
}

internal sealed record SnapshotPageData(
    IReadOnlyList<ClassifiedSnapshot> Branches,
    string? NextCursor);

internal sealed record ClassifiedSnapshot(
    BranchSnapshotEntity Branch,
    App.GitHealth.Core.Branches.BranchComparison Comparison);

internal sealed record SnapshotSelectionData(
    AnalysisRunEntity Analysis,
    IReadOnlyList<ClassifiedSnapshot> Branches,
    SnapshotPolicyResponse Policy);

internal sealed record SnapshotPageResponse
{
    public required Guid AnalysisId { get; init; }

    public required DateTimeOffset CapturedAtUtc { get; init; }

    public required string ReferenceName { get; init; }

    public required SnapshotPolicyResponse Policy { get; init; }

    public required IReadOnlyList<BranchSnapshotResponse> Items { get; init; }

    public string? NextCursor { get; init; }
}

internal sealed record BranchSnapshotResponse
{
    public required Guid Id { get; init; }

    public required string ReferenceName { get; init; }

    public required string CommitId { get; init; }

    public required int AheadCount { get; init; }

    public required int BehindCount { get; init; }

    public required string Relationship { get; init; }

    public DateTimeOffset? LastActivityAtUtc { get; init; }

    public string? TipAuthor { get; init; }

    public required string Topology { get; init; }

    public required string Activity { get; init; }

    public required string Recommendation { get; init; }

    public required string Reason { get; init; }

    public required bool IsProtected { get; init; }

    public required bool IsExcluded { get; init; }
}

internal sealed record ContributorResponse(string Name, string Email, int CommitCount);

internal sealed record SnapshotDetailResponse
{
    public required Guid AnalysisId { get; init; }

    public required string ReferenceName { get; init; }

    public required string ReferenceCommit { get; init; }

    public required DateTimeOffset CapturedAtUtc { get; init; }

    public required BranchSnapshotResponse Snapshot { get; init; }

    public required IReadOnlyList<ContributorResponse> Contributors { get; init; }

    public required string AttributionStatus { get; init; }

    public required bool MailmapApplied { get; init; }

    public required SnapshotPolicyResponse Policy { get; init; }
}

internal sealed record SnapshotPolicyResponse
{
    public required int ActiveUntilDays { get; init; }

    public required int InactiveAfterDays { get; init; }

    public required IReadOnlyList<string> ExcludedPatterns { get; init; }

    public required IReadOnlyList<string> ProtectedPatterns { get; init; }
}

internal enum AttributionStatus
{
    Available,
    UnavailableAfterMerge,
}
