using App.GitHealth.Core.Branches;

namespace App.GitHealth.Core.Tests.Branches;

public sealed class GitRefTests
{
    [Theory]
    [InlineData("refs/heads/feature/unicode-é", GitRefKind.LocalBranch)]
    [InlineData("refs/remotes/origin/main", GitRefKind.RemoteBranch)]
    public void ConstructorAcceptsFullBranchReferences(string value, GitRefKind expectedKind)
    {
        var reference = new GitRef(value);

        Assert.Equal(expectedKind, reference.Kind);
    }

    [Theory]
    [InlineData("refs/")]
    [InlineData("refs/tags/v1")]
    [InlineData("refs/heads/")]
    [InlineData("refs/heads/feature with space")]
    [InlineData("refs/heads/bad..name")]
    [InlineData("refs/heads/.hidden")]
    public void ConstructorRejectsReferencesOutsideTheSupportedNamespaces(string value)
    {
        Assert.Throws<ArgumentException>(() => new GitRef(value));
    }
}
