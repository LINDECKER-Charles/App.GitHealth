using App.GitHealth.Core.Branches;

namespace App.GitHealth.Api.Persistence.Entities;

internal sealed class ContributorSnapshotEntity
{
    private ContributorSnapshotEntity()
    {
    }

    public Guid Id { get; private set; }

    public Guid BranchSnapshotId { get; private set; }

    public BranchSnapshotEntity BranchSnapshot { get; private set; } = null!;

    public string Name { get; private set; } = string.Empty;

    public string Email { get; private set; } = string.Empty;

    public int CommitCount { get; private set; }

    public static ContributorSnapshotEntity Create(Guid branchId, Contributor contributor)
    {
        ArgumentNullException.ThrowIfNull(contributor);
        return new ContributorSnapshotEntity
        {
            Id = Guid.NewGuid(),
            BranchSnapshotId = branchId,
            Name = contributor.Name,
            Email = contributor.Email,
            CommitCount = contributor.CommitCount,
        };
    }
}
