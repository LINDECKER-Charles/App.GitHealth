using App.GitHealth.Core.Branches;

namespace App.GitHealth.Core.Tests.Branches;

public sealed class ContributorTests
{
    [Fact]
    public void ConstructorRejectsAnEmptyIdentityOrCommitCount()
    {
        Assert.Throws<ArgumentException>(() => new Contributor("", "ada@example.test", 1));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new Contributor("Ada", "ada@example.test", 0));
    }
}
