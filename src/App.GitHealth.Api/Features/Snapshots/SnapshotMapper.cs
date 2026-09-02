using System.Text.Json;
using App.GitHealth.Api.Persistence.Entities;
using App.GitHealth.Core.Branches;
using App.GitHealth.Core.Common;

namespace App.GitHealth.Api.Features.Snapshots;

internal sealed class SnapshotMapper(IClock clock)
{
    private const bool MailmapApplied = true;

    public ClassifiedSnapshot Classify(
        BranchSnapshotEntity branch,
        ActivityThresholds thresholds,
        BranchPolicy policy)
    {
        var facts = MapFacts(branch);
        var comparison = new BranchClassifier(clock).Classify(facts, thresholds, policy);
        return new ClassifiedSnapshot(branch, comparison);
    }

    public static ClassifiedSnapshot ClassifyCaptured(
        AnalysisRunEntity analysis,
        BranchSnapshotEntity branch)
    {
        var capturedAt = analysis.CapturedAtUtc
            ?? throw new InvalidOperationException("The analysis has no capture date.");
        var classifier = new BranchClassifier(new CapturedAnalysisClock(capturedAt));
        var comparison = classifier.Classify(
            MapFacts(branch),
            CapturedThresholds(analysis),
            CapturedPolicy(analysis));
        return new ClassifiedSnapshot(branch, comparison);
    }

    public static BranchSnapshotResponse Map(ClassifiedSnapshot classified)
    {
        var branch = classified.Branch;
        var comparison = classified.Comparison;
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
            TopContributor = MapContributors(branch).FirstOrDefault(),
        };
    }

    public static SnapshotDetailResponse MapDetail(BranchSnapshotEntity branch)
    {
        var analysis = branch.AnalysisRun;
        var classified = ClassifyCaptured(analysis, branch);
        var contributors = MapContributors(branch);
        return new SnapshotDetailResponse
        {
            AnalysisId = analysis.Id,
            ReferenceName = analysis.ReferenceName,
            ReferenceCommit = analysis.ReferenceCommit!,
            CapturedAtUtc = analysis.CapturedAtUtc!.Value,
            Snapshot = Map(classified),
            Contributors = contributors,
            AttributionStatus = GetAttributionStatus(classified, contributors.Length).ToString(),
            MailmapApplied = MailmapApplied,
            Policy = MapPolicy(analysis),
        };
    }

    private static AttributionStatus GetAttributionStatus(
        ClassifiedSnapshot classified,
        int contributorCount) => contributorCount == 0
            && classified.Comparison.Topology == BranchTopology.Merged
                ? AttributionStatus.UnavailableAfterMerge
                : AttributionStatus.Available;

    public static SnapshotPolicyResponse MapPolicy(AnalysisRunEntity analysis) => new()
    {
        ActiveUntilDays = analysis.ActiveUntilDays,
        InactiveAfterDays = analysis.InactiveAfterDays,
        ExcludedPatterns = ReadPatterns(analysis.ExcludedPatternsJson),
        ProtectedPatterns = ReadPatterns(analysis.ProtectedPatternsJson),
    };

    internal static BranchFacts MapFacts(BranchSnapshotEntity branch)
    {
        return new BranchFacts(
            new GitRef(branch.ReferenceName),
            BranchDivergence.Create(
                branch.AheadCount,
                branch.BehindCount,
                branch.Relationship),
            new BranchTip(
                new CommitId(branch.CommitId),
                branch.LastActivityAtUtc,
                branch.TipAuthor));
    }

    private static ActivityThresholds CapturedThresholds(AnalysisRunEntity analysis) =>
        ActivityThresholds.Create(
            analysis.ActiveUntilDays,
            analysis.InactiveAfterDays);

    private static BranchPolicy CapturedPolicy(AnalysisRunEntity analysis) =>
        BranchPolicy.Create(
            ReadPatterns(analysis.ExcludedPatternsJson),
            ReadPatterns(analysis.ProtectedPatternsJson));

    private static ContributorResponse[] MapContributors(
        BranchSnapshotEntity branch) => branch.Contributors
            .OrderByDescending(contributor => contributor.CommitCount)
            .ThenBy(contributor => contributor.Email, StringComparer.Ordinal)
            .Select(contributor => new ContributorResponse(
                contributor.Name,
                contributor.Email,
                contributor.CommitCount))
            .ToArray();

    private static string[] ReadPatterns(string json) =>
        JsonSerializer.Deserialize<string[]>(json) ?? [];

    private sealed record CapturedAnalysisClock(DateTimeOffset UtcNow) : IClock;
}
