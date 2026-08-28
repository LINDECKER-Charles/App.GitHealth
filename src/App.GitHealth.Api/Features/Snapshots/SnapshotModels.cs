using App.GitHealth.Api.Persistence.Entities;
using Microsoft.AspNetCore.Mvc;

namespace App.GitHealth.Api.Features.Snapshots;

internal sealed record SnapshotQueryParameters
{
    [FromQuery(Name = "search")]
    public string? Search { get; init; }

    [FromQuery(Name = "relationship")]
    public string? Relationship { get; init; }

    [FromQuery(Name = "sort")]
    public string Sort { get; init; } = "name";

    [FromQuery(Name = "direction")]
    public string Direction { get; init; } = "asc";

    [FromQuery(Name = "cursor")]
    public string? Cursor { get; init; }

    [FromQuery(Name = "pageSize")]
    public int PageSize { get; init; } = 50;
}

internal sealed record SnapshotCursorData
{
    public required Guid AnalysisId { get; init; }

    public required string Sort { get; init; }

    public required string Direction { get; init; }

    public string? Search { get; init; }

    public string? Relationship { get; init; }

    public required string SortValue { get; init; }

    public required string ReferenceName { get; init; }

    public required Guid SnapshotId { get; init; }
}

internal sealed record SnapshotPageData(
    IReadOnlyList<BranchSnapshotEntity> Branches,
    string? NextCursor);

internal sealed record SnapshotPageResponse
{
    public required Guid AnalysisId { get; init; }

    public required DateTimeOffset CapturedAtUtc { get; init; }

    public required string ReferenceName { get; init; }

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
}
