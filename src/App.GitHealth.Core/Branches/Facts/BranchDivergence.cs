namespace App.GitHealth.Core.Branches;

public sealed record BranchDivergence
{
    private BranchDivergence(int aheadCount, int behindCount, BranchRelationship relationship)
    {
        AheadCount = aheadCount;
        BehindCount = behindCount;
        Relationship = relationship;
    }

    public int AheadCount { get; }

    public int BehindCount { get; }

    public BranchRelationship Relationship { get; }

    public static BranchDivergence Create(
        int aheadCount,
        int behindCount,
        BranchRelationship relationship)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(aheadCount);
        ArgumentOutOfRangeException.ThrowIfNegative(behindCount);
        ValidateRelationship(aheadCount, behindCount, relationship);
        return new BranchDivergence(aheadCount, behindCount, relationship);
    }

    private static void ValidateRelationship(
        int aheadCount,
        int behindCount,
        BranchRelationship relationship)
    {
        var isValid = relationship switch
        {
            BranchRelationship.SameCommit => aheadCount == 0 && behindCount == 0,
            BranchRelationship.BranchIsAncestorOfReference =>
                aheadCount == 0 && behindCount > 0,
            BranchRelationship.CommonAncestor => aheadCount > 0,
            BranchRelationship.NoCommonAncestor => aheadCount > 0 && behindCount > 0,
            _ => false,
        };

        if (!isValid)
        {
            throw new ArgumentException(
                "The Git relationship is inconsistent with the ahead and behind counters.",
                nameof(relationship));
        }
    }
}
