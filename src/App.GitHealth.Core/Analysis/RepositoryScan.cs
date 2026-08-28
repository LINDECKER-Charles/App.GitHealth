using App.GitHealth.Core.Branches;

namespace App.GitHealth.Core.Analysis;

public sealed record RepositoryScan
{
    public RepositoryScan(
        RepositoryScanMetadata metadata,
        CommitId referenceCommit,
        IEnumerable<ScannedBranch> branches)
    {
        ArgumentNullException.ThrowIfNull(metadata);
        ArgumentNullException.ThrowIfNull(referenceCommit);
        ArgumentNullException.ThrowIfNull(branches);

        Metadata = metadata;
        ReferenceCommit = referenceCommit;
        Branches = Array.AsReadOnly(branches.ToArray());
    }

    public RepositoryScanMetadata Metadata { get; }

    public CommitId ReferenceCommit { get; }

    public IReadOnlyList<ScannedBranch> Branches { get; }
}
