namespace App.GitHealth.Core.Branches;

public sealed record BranchTip
{
    public BranchTip(CommitId commit, DateTimeOffset? lastActivityAt, string? author)
    {
        ArgumentNullException.ThrowIfNull(commit);

        if (lastActivityAt is { Offset: var offset } && offset != TimeSpan.Zero)
        {
            throw new ArgumentException(
                "La date d’activité doit être en UTC.",
                nameof(lastActivityAt));
        }

        Commit = commit;
        LastActivityAt = lastActivityAt;
        Author = string.IsNullOrWhiteSpace(author) ? null : author.Trim();
    }

    public CommitId Commit { get; }

    public DateTimeOffset? LastActivityAt { get; }

    public string? Author { get; }
}
