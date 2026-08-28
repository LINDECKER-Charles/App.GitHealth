using App.GitHealth.Core.Branches;

namespace App.GitHealth.Core.Analysis;

public sealed record RepositoryDescriptor
{
    public RepositoryDescriptor(
        RepositoryLocation location,
        GitRef? suggestedReference,
        IEnumerable<GitRef> references)
    {
        ArgumentNullException.ThrowIfNull(location);
        ArgumentNullException.ThrowIfNull(references);

        Location = location;
        SuggestedReference = suggestedReference;
        References = Array.AsReadOnly(references.ToArray());
    }

    public RepositoryLocation Location { get; }

    public GitRef? SuggestedReference { get; }

    public IReadOnlyList<GitRef> References { get; }
}
