namespace App.GitHealth.Core.Branches;

public sealed record BranchFacts
{
    public BranchFacts(GitRef reference, BranchDivergence divergence, BranchTip tip)
    {
        ArgumentNullException.ThrowIfNull(reference);
        ArgumentNullException.ThrowIfNull(divergence);
        ArgumentNullException.ThrowIfNull(tip);

        Reference = reference;
        Divergence = divergence;
        Tip = tip;
    }

    public GitRef Reference { get; }

    public BranchDivergence Divergence { get; }

    public BranchTip Tip { get; }

    public CommitId Commit => Tip.Commit;

    public int AheadCount => Divergence.AheadCount;

    public int BehindCount => Divergence.BehindCount;

    public DateTimeOffset? LastActivityAt => Tip.LastActivityAt;

    public string? TipAuthor => Tip.Author;
}
