using App.GitHealth.Core.Branches;

namespace App.GitHealth.Core.Tests.Branches;

public sealed class BranchPolicyValidationTests
{
    [Fact]
    public void CreateCopiesAndDeduplicatesPatterns()
    {
        var source = new List<string> { "refs/heads/release/*", "refs/heads/release/*" };

        var policy = BranchPolicy.Create([], source);
        source.Clear();

        Assert.Single(policy.ProtectedPatterns);
    }

    [Fact]
    public void MatchingIsCaseSensitiveAndUsesTheFullReferenceName()
    {
        var policy = BranchPolicy.Create([], ["refs/heads/Feature/*"]);
        var classifier = new BranchClassifier(
            new TestClock(new DateTimeOffset(2026, 8, 28, 12, 0, 0, TimeSpan.Zero)));

        var result = classifier.Classify(
            BranchFactsBuilder.Create(0, 2, true),
            ActivityThresholds.Default,
            policy);

        Assert.False(result.IsProtected);
    }
}
