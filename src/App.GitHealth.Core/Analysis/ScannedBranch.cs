using App.GitHealth.Core.Branches;

namespace App.GitHealth.Core.Analysis;

public sealed record ScannedBranch
{
    public ScannedBranch(BranchFacts facts, IEnumerable<Contributor> contributors)
    {
        ArgumentNullException.ThrowIfNull(facts);
        ArgumentNullException.ThrowIfNull(contributors);

        Facts = facts;
        Contributors = Array.AsReadOnly(contributors.ToArray());
    }

    public BranchFacts Facts { get; }

    public IReadOnlyList<Contributor> Contributors { get; }
}
