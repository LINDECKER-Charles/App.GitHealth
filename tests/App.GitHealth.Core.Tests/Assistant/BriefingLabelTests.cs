using App.GitHealth.Core.Assistant;

namespace App.GitHealth.Core.Tests.Assistant;

public sealed class BriefingLabelTests
{
    /// <summary>
    /// These values are read, not parsed: a person sees them in the panel and an agent sees
    /// them in a tool answer. Flattening the enum name to lower case used to produce
    /// "branchisancestorofreference", which neither of them can read.
    /// </summary>
    [Theory]
    [InlineData("BranchIsAncestorOfReference", "branch is ancestor of reference")]
    [InlineData("CleanupCandidate", "cleanup candidate")]
    [InlineData("CommonAncestor", "common ancestor")]
    [InlineData("SameCommit", "same commit")]
    [InlineData("NoCommonAncestor", "no common ancestor")]
    public void ACompoundNameIsSpeltAsWords(string value, string expected)
    {
        Assert.Equal(expected, BriefingLabel.Words(value));
    }

    [Theory]
    [InlineData("Merged", "merged")]
    [InlineData("Ahead", "ahead")]
    [InlineData("Keep", "keep")]
    public void ASingleWordOnlyLosesItsCapital(string value, string expected)
    {
        Assert.Equal(expected, BriefingLabel.Words(value));
    }

    [Fact]
    public void AnEmptyNameStaysEmptyRatherThanBecomingASpace()
    {
        Assert.Equal(string.Empty, BriefingLabel.Words(string.Empty));
    }
}
