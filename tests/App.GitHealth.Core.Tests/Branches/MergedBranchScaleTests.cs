using App.GitHealth.Core.Branches;

namespace App.GitHealth.Core.Tests.Branches;

/// <summary>
/// Une branche sans commit propre — fusionnée ou au même sommet que la référence — ne
/// détient plus rien que la référence n'ait déjà. Elle est donc mesurée sur une échelle
/// réduite, et n'est jamais présentée comme étant à conserver une fois le délai passé.
/// </summary>
public sealed class MergedBranchScaleTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 28, 12, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData(3, ActivityStatus.Active, RecommendationKind.Merged)]
    [InlineData(7, ActivityStatus.Active, RecommendationKind.Merged)]
    [InlineData(8, ActivityStatus.Aging, RecommendationKind.Review)]
    [InlineData(30, ActivityStatus.Aging, RecommendationKind.Review)]
    [InlineData(31, ActivityStatus.Inactive, RecommendationKind.CleanupCandidate)]
    public void MergedBranchFollowsTheReducedScale(
        int ageInDays,
        ActivityStatus expectedActivity,
        RecommendationKind expectedRecommendation)
    {
        var result = ClassifyAged(BranchFactsBuilder.Create(0, 4, true), ageInDays);

        Assert.Equal(BranchTopology.Merged, result.Topology);
        Assert.Equal(expectedActivity, result.Activity);
        Assert.Equal(expectedRecommendation, result.Recommendation);
    }

    [Theory]
    [InlineData(3, RecommendationKind.Merged)]
    [InlineData(20, RecommendationKind.Review)]
    [InlineData(60, RecommendationKind.CleanupCandidate)]
    public void SynchronizedBranchFollowsTheSameScale(
        int ageInDays,
        RecommendationKind expected)
    {
        var result = ClassifyAged(BranchFactsBuilder.Create(), ageInDays);

        Assert.Equal(BranchTopology.Synchronized, result.Topology);
        Assert.Equal(expected, result.Recommendation);
    }

    [Fact]
    public void MergedBranchIsNeverKeptOnceTheReducedDelayHasPassed()
    {
        var result = ClassifyAged(BranchFactsBuilder.Create(0, 4, true), 45);

        Assert.NotEqual(RecommendationKind.Keep, result.Recommendation);
        Assert.Contains("Aucun commit propre", result.Reason, StringComparison.Ordinal);
    }

    /// <summary>
    /// « Conserver » signale qu'il y a quelque chose à préserver. Une branche sans commit
    /// propre n'entre jamais dans cet état : elle est terminée, pas à protéger.
    /// </summary>
    [Theory]
    [InlineData(1)]
    [InlineData(14)]
    [InlineData(120)]
    public void BranchWithoutOwnCommitsIsNeverRecommendedForKeeping(int ageInDays)
    {
        var merged = ClassifyAged(BranchFactsBuilder.Create(0, 4, true), ageInDays);
        var synchronized = ClassifyAged(BranchFactsBuilder.Create(), ageInDays);

        Assert.NotEqual(RecommendationKind.Keep, merged.Recommendation);
        Assert.NotEqual(RecommendationKind.Keep, synchronized.Recommendation);
    }

    [Fact]
    public void BranchWithOwnCommitsKeepsTheProjectScale()
    {
        var result = ClassifyAged(BranchFactsBuilder.Create(3, 4), 45);

        Assert.Equal(BranchTopology.Diverged, result.Topology);
        Assert.Equal(ActivityStatus.Aging, result.Activity);
        Assert.Equal(RecommendationKind.Review, result.Recommendation);
    }

    [Fact]
    public void UnrelatedHistoryNeverBorrowsTheReducedScale()
    {
        var divergence = BranchDivergence.Create(2, 2, BranchRelationship.NoCommonAncestor);
        var facts = new BranchFacts(
            new GitRef("refs/heads/orphan"),
            divergence,
            new BranchTip(new CommitId("abcdef"), Now.AddDays(-45), null));

        var result = new BranchClassifier(new TestClock(Now))
            .Classify(facts, ActivityThresholds.Default, BranchPolicy.Empty);

        Assert.Equal(ActivityStatus.Aging, result.Activity);
    }

    [Fact]
    public void ProtectedMergedBranchStaysOutOfTheRecommendations()
    {
        var policy = BranchPolicy.Create([], ["refs/heads/feature/*"]);
        var result = ClassifyAged(BranchFactsBuilder.Create(0, 4, true), 200, policy);

        Assert.Equal(RecommendationKind.Excluded, result.Recommendation);
    }

    [Theory]
    [InlineData(30, 90, 7, 30)]
    [InlineData(3, 10, 3, 10)]
    [InlineData(20, 25, 7, 25)]
    [InlineData(2, 40, 2, 30)]
    public void ReducedScaleNeverLengthensATighterProjectScale(
        int projectActive,
        int projectInactive,
        int expectedActive,
        int expectedInactive)
    {
        var applied = BranchClassifier.AppliedThresholds(
            BranchTopology.Merged,
            ActivityThresholds.Create(projectActive, projectInactive));

        Assert.Equal(expectedActive, applied.ActiveUntilDays);
        Assert.Equal(expectedInactive, applied.InactiveAfterDays);
    }

    private static BranchComparison ClassifyAged(
        BranchFacts facts,
        int ageInDays,
        BranchPolicy? policy = null)
    {
        var tip = new BranchTip(facts.Commit, Now.AddDays(-ageInDays), facts.TipAuthor);
        var aged = new BranchFacts(facts.Reference, facts.Divergence, tip);
        return new BranchClassifier(new TestClock(Now))
            .Classify(aged, ActivityThresholds.Default, policy ?? BranchPolicy.Empty);
    }
}
