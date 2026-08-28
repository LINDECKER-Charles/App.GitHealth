using App.GitHealth.Core.Analysis;
using App.GitHealth.Core.Branches;

namespace App.GitHealth.Api.Persistence.Entities;

internal sealed class BranchSnapshotEntity
{
    private BranchSnapshotEntity()
    {
    }

    public Guid Id { get; private set; }

    public Guid AnalysisRunId { get; private set; }

    public AnalysisRunEntity AnalysisRun { get; private set; } = null!;

    public string ReferenceName { get; private set; } = string.Empty;

    public string CommitId { get; private set; } = string.Empty;

    public int AheadCount { get; private set; }

    public int BehindCount { get; private set; }

    public BranchRelationship Relationship { get; private set; }

    public DateTimeOffset? LastActivityAtUtc { get; private set; }

    public string? TipAuthor { get; private set; }

    public ICollection<ContributorSnapshotEntity> Contributors { get; } = [];

    public static BranchSnapshotEntity Create(Guid analysisId, ScannedBranch branch)
    {
        ArgumentNullException.ThrowIfNull(branch);
        var facts = branch.Facts;
        var entity = new BranchSnapshotEntity
        {
            Id = Guid.NewGuid(),
            AnalysisRunId = analysisId,
            ReferenceName = facts.Reference.FullName,
            CommitId = facts.Commit.Value,
            AheadCount = facts.AheadCount,
            BehindCount = facts.BehindCount,
            Relationship = facts.Divergence.Relationship,
            LastActivityAtUtc = facts.LastActivityAt,
            TipAuthor = facts.TipAuthor,
        };
        entity.AddContributors(branch.Contributors);
        return entity;
    }

    private void AddContributors(IEnumerable<Contributor> contributors)
    {
        foreach (var contributor in contributors)
        {
            Contributors.Add(ContributorSnapshotEntity.Create(Id, contributor));
        }
    }
}
