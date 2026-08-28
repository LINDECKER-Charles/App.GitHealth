namespace App.GitHealth.Core.Branches;

public sealed record BranchComparison
{
    public required BranchFacts Facts { get; init; }

    public required BranchTopology Topology { get; init; }

    public required ActivityStatus Activity { get; init; }

    public required RecommendationKind Recommendation { get; init; }

    public bool IsProtected { get; init; }

    public bool IsExcluded { get; init; }

    public string Reason { get; init; } = string.Empty;
}
