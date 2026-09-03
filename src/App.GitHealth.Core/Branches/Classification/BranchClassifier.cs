using System.IO.Enumeration;
using App.GitHealth.Core.Common;

namespace App.GitHealth.Core.Branches;

public sealed class BranchClassifier(IClock clock)
{
    public BranchComparison Classify(
        BranchFacts facts,
        ActivityThresholds thresholds,
        BranchPolicy policy)
    {
        ArgumentNullException.ThrowIfNull(facts);
        ArgumentNullException.ThrowIfNull(thresholds);
        ArgumentNullException.ThrowIfNull(policy);

        var topology = ClassifyTopology(facts.Divergence);
        var applied = AppliedThresholds(topology, thresholds);
        var assessment = new Assessment(
            topology,
            ClassifyActivity(facts.LastActivityAt, applied),
            Match(facts.Reference.FullName, policy.ProtectedPatterns),
            Match(facts.Reference.FullName, policy.ExcludedPatterns));

        return new BranchComparison
        {
            Facts = facts,
            Topology = assessment.Topology,
            Activity = assessment.Activity,
            Recommendation = Recommend(assessment),
            IsProtected = assessment.Protection is not null,
            IsExcluded = assessment.Exclusion is not null,
            Reason = Explain(assessment),
        };
    }

    /// <summary>
    /// A branch with no own commits is measured on the shortened scale: the baseline
    /// already contains its whole history, so nothing is lost by deleting it.
    /// </summary>
    public static ActivityThresholds AppliedThresholds(
        BranchTopology topology,
        ActivityThresholds thresholds)
    {
        ArgumentNullException.ThrowIfNull(thresholds);
        return HasOwnCommits(topology) ? thresholds : thresholds.ShortenTo(ActivityThresholds.Merged);
    }

    /// <summary>
    /// False when every commit of the branch is already reachable from the baseline:
    /// same commit, or branch strictly an ancestor.
    /// </summary>
    public static bool HasOwnCommits(BranchTopology topology)
    {
        return topology is not (BranchTopology.Merged or BranchTopology.Synchronized);
    }

    /// <summary>
    /// Where a reference sits against the baseline. Public because a running scan names the
    /// topology of each reference as soon as it is measured, long before the verdict.
    /// </summary>
    public static BranchTopology ClassifyTopology(BranchDivergence divergence)
    {
        ArgumentNullException.ThrowIfNull(divergence);

        if (divergence.Relationship == BranchRelationship.NoCommonAncestor)
        {
            return BranchTopology.Unrelated;
        }

        if (divergence.Relationship == BranchRelationship.SameCommit)
        {
            return BranchTopology.Synchronized;
        }

        if (divergence.AheadCount > 0 && divergence.BehindCount == 0)
        {
            return BranchTopology.Ahead;
        }

        return divergence.Relationship == BranchRelationship.BranchIsAncestorOfReference
            ? BranchTopology.Merged
            : BranchTopology.Diverged;
    }

    private ActivityStatus ClassifyActivity(
        DateTimeOffset? lastActivityAt,
        ActivityThresholds thresholds)
    {
        if (lastActivityAt is null)
        {
            return ActivityStatus.Unknown;
        }

        var age = clock.UtcNow - lastActivityAt.Value;
        var ageInDays = Math.Max(0, age.TotalDays);

        if (ageInDays <= thresholds.ActiveUntilDays)
        {
            return ActivityStatus.Active;
        }

        return ageInDays <= thresholds.InactiveAfterDays
            ? ActivityStatus.Aging
            : ActivityStatus.Inactive;
    }

    private static RecommendationKind Recommend(Assessment assessment)
    {
        if (assessment.IsCaptured)
        {
            return RecommendationKind.Excluded;
        }

        if (!assessment.HasOwnCommits)
        {
            return RecommendWithoutOwnCommits(assessment.Activity);
        }

        return assessment.Activity == ActivityStatus.Inactive
            || assessment.Topology is BranchTopology.Diverged or BranchTopology.Unrelated
            ? RecommendationKind.Review
            : RecommendationKind.Keep;
    }

    private static RecommendationKind RecommendWithoutOwnCommits(ActivityStatus activity)
    {
        return activity switch
        {
            ActivityStatus.Inactive => RecommendationKind.CleanupCandidate,
            ActivityStatus.Aging => RecommendationKind.Review,
            _ => RecommendationKind.Merged,
        };
    }

    private static string Explain(Assessment assessment)
    {
        if (assessment.Protection is not null)
        {
            return $"Protected by pattern \"{assessment.Protection}\"";
        }

        if (assessment.Exclusion is not null)
        {
            return $"Excluded by pattern \"{assessment.Exclusion}\"";
        }

        return assessment.HasOwnCommits
            ? ExplainOwnCommits(assessment)
            : ExplainWithoutOwnCommits(assessment.Activity);
    }

    private static string ExplainWithoutOwnCommits(ActivityStatus activity)
    {
        return activity switch
        {
            ActivityStatus.Inactive =>
                "No own commits and no activity for a long time: "
                + "candidate for manual cleanup",
            ActivityStatus.Aging =>
                "No own commits: the baseline already contains its whole history",
            ActivityStatus.Active =>
                "Done: the baseline already contains everything, the deadline is still running",
            _ => "No own commits, with no usable tip date",
        };
    }

    private static string ExplainOwnCommits(Assessment assessment)
    {
        return (assessment.Topology, assessment.Activity) switch
        {
            (_, ActivityStatus.Inactive) => "Inactive, with Git facts to review",
            (BranchTopology.Diverged, _) => "Diverged history to review",
            (BranchTopology.Unrelated, _) => "No common ancestor with the baseline",
            _ => "No action recommended",
        };
    }

    private static string? Match(string reference, IReadOnlyList<string> patterns)
    {
        return patterns.FirstOrDefault(pattern =>
            FileSystemName.MatchesSimpleExpression(pattern, reference, ignoreCase: false));
    }

    private readonly record struct Assessment(
        BranchTopology Topology,
        ActivityStatus Activity,
        string? Protection,
        string? Exclusion)
    {
        public bool IsCaptured => Protection is not null || Exclusion is not null;

        public bool HasOwnCommits => BranchClassifier.HasOwnCommits(Topology);
    }
}
