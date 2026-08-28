using App.GitHealth.Core.Branches;

namespace App.GitHealth.Core.Tests.Branches;

public sealed class ActivityThresholdsTests
{
    private static readonly DateTimeOffset Now =
        new(2026, 8, 28, 12, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData(30, ActivityStatus.Active)]
    [InlineData(31, ActivityStatus.Aging)]
    [InlineData(90, ActivityStatus.Aging)]
    [InlineData(91, ActivityStatus.Inactive)]
    public void ClassifyHonorsThresholdBoundaries(int ageInDays, ActivityStatus expected)
    {
        var classifier = new BranchClassifier(new TestClock(Now));
        var original = BranchFactsBuilder.Create();
        var tip = new BranchTip(original.Commit, Now.AddDays(-ageInDays), original.TipAuthor);
        var facts = new BranchFacts(original.Reference, original.Divergence, tip);

        var result = classifier.Classify(facts, ActivityThresholds.Default, BranchPolicy.Empty);

        Assert.Equal(expected, result.Activity);
    }

    [Theory]
    [InlineData(30, ActivityStatus.Aging)]
    [InlineData(90, ActivityStatus.Inactive)]
    public void ClassifyChangesImmediatelyAfterThreshold(int threshold, ActivityStatus expected)
    {
        var classifier = new BranchClassifier(new TestClock(Now));
        var original = BranchFactsBuilder.Create();
        var tip = new BranchTip(original.Commit, Now.AddDays(-threshold).AddTicks(-1), null);
        var facts = new BranchFacts(original.Reference, original.Divergence, tip);

        var result = classifier.Classify(facts, ActivityThresholds.Default, BranchPolicy.Empty);

        Assert.Equal(expected, result.Activity);
    }

    [Theory]
    [InlineData(-1, 90)]
    [InlineData(30, 30)]
    [InlineData(31, 30)]
    public void CreateRejectsInvalidThresholds(int activeUntil, int inactiveAfter)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            ActivityThresholds.Create(activeUntil, inactiveAfter));
    }
}
