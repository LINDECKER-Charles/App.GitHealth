using App.GitHealth.Core.Branches;

namespace App.GitHealth.Core.Tests.Branches;

public sealed class BranchPolicyTests
{
    private static readonly DateTimeOffset Now =
        new(2026, 8, 28, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void ProtectedMergedInactiveBranchIsNeverACleanupCandidate()
    {
        var policy = BranchPolicy.Create([], ["refs/heads/feature/*"]);
        var result = ClassifyInactive(BranchFactsBuilder.Create(0, 4, true), policy);

        Assert.Equal(RecommendationKind.Excluded, result.Recommendation);
        Assert.True(result.IsProtected);
        Assert.Contains("refs/heads/feature/*", result.Reason, StringComparison.Ordinal);
    }

    [Fact]
    public void InactiveUnmergedBranchIsOnlyMarkedForReview()
    {
        var result = ClassifyInactive(BranchFactsBuilder.Create(3), BranchPolicy.Empty);

        Assert.Equal(RecommendationKind.Review, result.Recommendation);
        Assert.NotEqual(RecommendationKind.CleanupCandidate, result.Recommendation);
    }

    [Fact]
    public void ExclusionDoesNotChangeGitTopology()
    {
        var policy = BranchPolicy.Create(["refs/heads/feature/*"], []);
        var result = ClassifyInactive(BranchFactsBuilder.Create(0, 4, true), policy);

        Assert.Equal(BranchTopology.Merged, result.Topology);
        Assert.True(result.IsExcluded);
        Assert.Equal(RecommendationKind.Excluded, result.Recommendation);
    }

    [Fact]
    public void InactiveMergedUnprotectedBranchIsACleanupCandidate()
    {
        var result = ClassifyInactive(BranchFactsBuilder.Create(0, 4, true), BranchPolicy.Empty);

        Assert.Equal(RecommendationKind.CleanupCandidate, result.Recommendation);
    }

    [Fact]
    public void ProtectionTakesPrecedenceWhenBothPoliciesMatch()
    {
        var pattern = "refs/heads/feature/*";
        var policy = BranchPolicy.Create([pattern], [pattern]);
        var result = ClassifyInactive(BranchFactsBuilder.Create(0, 4, true), policy);

        Assert.True(result.IsExcluded);
        Assert.True(result.IsProtected);
        Assert.StartsWith("Protected", result.Reason, StringComparison.Ordinal);
    }

    private static BranchComparison ClassifyInactive(BranchFacts facts, BranchPolicy policy)
    {
        var classifier = new BranchClassifier(new TestClock(Now));
        var tip = new BranchTip(facts.Commit, Now.AddDays(-91), facts.TipAuthor);
        var inactiveFacts = new BranchFacts(facts.Reference, facts.Divergence, tip);
        return classifier.Classify(inactiveFacts, ActivityThresholds.Default, policy);
    }
}
