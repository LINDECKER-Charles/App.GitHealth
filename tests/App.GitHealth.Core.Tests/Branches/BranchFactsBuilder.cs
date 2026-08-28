using App.GitHealth.Core.Branches;

namespace App.GitHealth.Core.Tests.Branches;

internal static class BranchFactsBuilder
{
    public static BranchFacts Create(
        int ahead = 0,
        int behind = 0,
        bool isAncestor = false)
    {
        var relationship = ResolveRelationship(ahead, behind, isAncestor);
        var divergence = BranchDivergence.Create(ahead, behind, relationship);
        var tip = new BranchTip(
            new CommitId("abcdef"),
            new DateTimeOffset(2026, 8, 28, 10, 0, 0, TimeSpan.Zero),
            "Ada Lovelace");

        return new BranchFacts(new GitRef("refs/heads/feature/demo"), divergence, tip);
    }

    private static BranchRelationship ResolveRelationship(int ahead, int behind, bool isAncestor)
    {
        if (ahead == 0 && behind == 0)
        {
            return BranchRelationship.SameCommit;
        }

        if (isAncestor)
        {
            return BranchRelationship.BranchIsAncestorOfReference;
        }

        return BranchRelationship.CommonAncestor;
    }
}
