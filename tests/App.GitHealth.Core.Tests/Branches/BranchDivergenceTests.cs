using App.GitHealth.Core.Branches;

namespace App.GitHealth.Core.Tests.Branches;

public sealed class BranchDivergenceTests
{
    [Theory]
    [InlineData(-1, 0, BranchRelationship.CommonAncestor)]
    [InlineData(1, -1, BranchRelationship.CommonAncestor)]
    [InlineData(1, 0, BranchRelationship.SameCommit)]
    [InlineData(1, 0, BranchRelationship.BranchIsAncestorOfReference)]
    [InlineData(0, 2, BranchRelationship.CommonAncestor)]
    [InlineData(1, 0, BranchRelationship.NoCommonAncestor)]
    public void CreateRejectsContradictoryFacts(
        int ahead,
        int behind,
        BranchRelationship relationship)
    {
        Assert.ThrowsAny<ArgumentException>(() =>
            BranchDivergence.Create(ahead, behind, relationship));
    }
}
