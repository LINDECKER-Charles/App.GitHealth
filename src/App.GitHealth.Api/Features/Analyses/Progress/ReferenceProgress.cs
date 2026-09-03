using App.GitHealth.Core.Branches;

namespace App.GitHealth.Api.Features.Analyses;

/// <summary>
/// One line of the ledger a running analysis fills in: what is known about a reference so
/// far. Everything past <see cref="CommitId"/> arrives as the stages go by.
/// </summary>
internal sealed record ReferenceProgress
{
    public required string ReferenceName { get; init; }

    public required string CommitId { get; init; }

    public required ReferenceProgressState State { get; init; }

    public DateTimeOffset? LastActivityAtUtc { get; init; }

    public string? TipAuthor { get; init; }

    public string? MergeBaseCommit { get; init; }

    public int? AheadCount { get; init; }

    public int? BehindCount { get; init; }

    public BranchTopology? Topology { get; init; }

    public string? TopContributor { get; init; }

    public int? ContributorCount { get; init; }
}
