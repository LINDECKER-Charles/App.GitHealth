using System.Text.Json;
using App.GitHealth.Api.Persistence.Entities;
using App.GitHealth.Core.Branches;
using App.GitHealth.Core.Common;

namespace App.GitHealth.Api.Features.Snapshots;

internal sealed class SnapshotMapper(IClock clock)
{
    public BranchSnapshotResponse Map(
        AnalysisRunEntity analysis,
        BranchSnapshotEntity branch)
    {
        var comparison = Classify(analysis, branch);
        return new BranchSnapshotResponse
        {
            Id = branch.Id,
            ReferenceName = branch.ReferenceName,
            CommitId = branch.CommitId,
            AheadCount = branch.AheadCount,
            BehindCount = branch.BehindCount,
            Relationship = branch.Relationship.ToString(),
            LastActivityAtUtc = branch.LastActivityAtUtc,
            TipAuthor = branch.TipAuthor,
            Topology = comparison.Topology.ToString(),
            Activity = comparison.Activity.ToString(),
            Recommendation = comparison.Recommendation.ToString(),
            Reason = comparison.Reason,
            IsProtected = comparison.IsProtected,
            IsExcluded = comparison.IsExcluded,
        };
    }

    public SnapshotDetailResponse MapDetail(BranchSnapshotEntity branch)
    {
        var analysis = branch.AnalysisRun;
        return new SnapshotDetailResponse
        {
            AnalysisId = analysis.Id,
            ReferenceName = analysis.ReferenceName,
            ReferenceCommit = analysis.ReferenceCommit!,
            CapturedAtUtc = analysis.CapturedAtUtc!.Value,
            Snapshot = Map(analysis, branch),
            Contributors = branch.Contributors
                .OrderByDescending(contributor => contributor.CommitCount)
                .ThenBy(contributor => contributor.Email, StringComparer.Ordinal)
                .Select(contributor => new ContributorResponse(
                    contributor.Name,
                    contributor.Email,
                    contributor.CommitCount))
                .ToArray(),
        };
    }

    private BranchComparison Classify(
        AnalysisRunEntity analysis,
        BranchSnapshotEntity branch)
    {
        var facts = new BranchFacts(
            new GitRef(branch.ReferenceName),
            BranchDivergence.Create(
                branch.AheadCount,
                branch.BehindCount,
                branch.Relationship),
            new BranchTip(
                new CommitId(branch.CommitId),
                branch.LastActivityAtUtc,
                branch.TipAuthor));
        var thresholds = ActivityThresholds.Create(
            analysis.ActiveUntilDays,
            analysis.InactiveAfterDays);
        var policy = BranchPolicy.Create(
            ReadPatterns(analysis.ExcludedPatternsJson),
            ReadPatterns(analysis.ProtectedPatternsJson));
        return new BranchClassifier(clock).Classify(facts, thresholds, policy);
    }

    private static string[] ReadPatterns(string json) =>
        JsonSerializer.Deserialize<string[]>(json) ?? [];
}
