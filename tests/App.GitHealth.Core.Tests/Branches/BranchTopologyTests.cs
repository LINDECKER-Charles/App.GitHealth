using App.GitHealth.Core.Branches;

namespace App.GitHealth.Core.Tests.Branches;

public sealed class BranchTopologyTests
{
    private static readonly DateTimeOffset Now =
        new(2026, 8, 28, 12, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData(0, 0, false, BranchTopology.Synchronized)]
    [InlineData(2, 0, false, BranchTopology.Ahead)]
    [InlineData(0, 3, true, BranchTopology.Merged)]
    [InlineData(2, 3, false, BranchTopology.Diverged)]
    public void ClassifyMapsAheadBehindAndAncestry(
        int ahead,
        int behind,
        bool isAncestor,
        BranchTopology expected)
    {
        var classifier = new BranchClassifier(new TestClock(Now));
        var facts = BranchFactsBuilder.Create(ahead, behind, isAncestor);

        var result = classifier.Classify(facts, ActivityThresholds.Default, BranchPolicy.Empty);

        Assert.Equal(expected, result.Topology);
    }

    [Fact]
    public void ClassifyReportsUnrelatedHistoriesExplicitly()
    {
        var classifier = new BranchClassifier(new TestClock(Now));
        var tip = new BranchTip(new CommitId("abcdef"), Now, "Ada Lovelace");
        var divergence = BranchDivergence.Create(1, 1, BranchRelationship.NoCommonAncestor);
        var facts = new BranchFacts(new GitRef("refs/heads/orphan"), divergence, tip);

        var result = classifier.Classify(facts, ActivityThresholds.Default, BranchPolicy.Empty);

        Assert.Equal(BranchTopology.Unrelated, result.Topology);
        Assert.Equal(RecommendationKind.Review, result.Recommendation);
    }
}
