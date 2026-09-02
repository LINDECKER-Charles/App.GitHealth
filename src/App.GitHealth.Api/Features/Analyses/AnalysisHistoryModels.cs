using Microsoft.AspNetCore.Mvc;

namespace App.GitHealth.Api.Features.Analyses;

internal sealed record AnalysisHistoryQueryParameters
{
    [FromQuery(Name = "page")]
    public int? Page { get; init; }

    [FromQuery(Name = "pageSize")]
    public int? PageSize { get; init; }

    /// <summary>Baseline whose history is read. Absent means every baseline of the project.</summary>
    [FromQuery(Name = "baseline")]
    public string? Baseline { get; init; }
}

internal sealed record AnalysisHistoryPageResponse
{
    public required IReadOnlyList<AnalysisHistoryItemResponse> Items { get; init; }

    public required int Page { get; init; }

    public required int PageSize { get; init; }

    public required int TotalCount { get; init; }
}

internal sealed record AnalysisHistoryItemResponse
{
    public required Guid AnalysisId { get; init; }

    public required string Status { get; init; }

    public required DateTimeOffset StartedAtUtc { get; init; }

    public DateTimeOffset? CompletedAtUtc { get; init; }

    public DateTimeOffset? CapturedAtUtc { get; init; }

    public required string ReferenceName { get; init; }

    public string? ReferenceCommit { get; init; }

    public required string BranchNamespace { get; init; }

    public required int ActiveUntilDays { get; init; }

    public required int InactiveAfterDays { get; init; }

    public required IReadOnlyList<string> ExcludedPatterns { get; init; }

    public required IReadOnlyList<string> ProtectedPatterns { get; init; }

    public string? GitVersion { get; init; }

    public required int BranchCount { get; init; }

    public string? FailureCode { get; init; }

    public string? FailureMessage { get; init; }
}
