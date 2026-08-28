using App.GitHealth.Core.Branches;

namespace App.GitHealth.Api.Git.Models;

internal sealed record CapturedReference
{
    public CapturedReference(GitRef reference, BranchTip tip, string? symbolicTarget)
    {
        Reference = reference;
        Tip = tip;
        SymbolicTarget = symbolicTarget;
    }

    public GitRef Reference { get; }

    public BranchTip Tip { get; }

    public string? SymbolicTarget { get; }

    public CommitId Commit => Tip.Commit;

    public DateTimeOffset? LastActivityAt => Tip.LastActivityAt;

    public string? TipAuthor => Tip.Author;
}
