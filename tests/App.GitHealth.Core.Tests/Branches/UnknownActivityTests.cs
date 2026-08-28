using App.GitHealth.Core.Branches;

namespace App.GitHealth.Core.Tests.Branches;

public sealed class UnknownActivityTests
{
    [Fact]
    public void MissingTipDateProducesUnknownActivity()
    {
        var clock = new TestClock(DateTimeOffset.UnixEpoch);
        var tip = new BranchTip(new CommitId("abcdef"), null, null);
        var facts = new BranchFacts(
            new GitRef("refs/heads/main"),
            BranchDivergence.Create(0, 0, BranchRelationship.SameCommit),
            tip);

        var result = new BranchClassifier(clock)
            .Classify(facts, ActivityThresholds.Default, BranchPolicy.Empty);

        Assert.Equal(ActivityStatus.Unknown, result.Activity);
    }
}
