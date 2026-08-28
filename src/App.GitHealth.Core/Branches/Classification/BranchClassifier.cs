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

        var topology = ClassifyTopology(facts);
        var activity = ClassifyActivity(facts.LastActivityAt, thresholds);
        var protection = Match(facts.Reference.FullName, policy.ProtectedPatterns);
        var exclusion = Match(facts.Reference.FullName, policy.ExcludedPatterns);
        var recommendation = Recommend(topology, activity, protection, exclusion);

        return new BranchComparison
        {
            Facts = facts,
            Topology = topology,
            Activity = activity,
            Recommendation = recommendation,
            IsProtected = protection is not null,
            IsExcluded = exclusion is not null,
            Reason = Explain(topology, activity, protection, exclusion),
        };
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

    private static BranchTopology ClassifyTopology(BranchFacts facts)
    {
        if (facts.Divergence.Relationship == BranchRelationship.NoCommonAncestor)
        {
            return BranchTopology.Unrelated;
        }

        if (facts.Divergence.Relationship == BranchRelationship.SameCommit)
        {
            return BranchTopology.Synchronized;
        }

        if (facts.AheadCount > 0 && facts.BehindCount == 0)
        {
            return BranchTopology.Ahead;
        }

        return facts.Divergence.Relationship == BranchRelationship.BranchIsAncestorOfReference
            ? BranchTopology.Merged
            : BranchTopology.Diverged;
    }

    private static RecommendationKind Recommend(
        BranchTopology topology,
        ActivityStatus activity,
        string? protection,
        string? exclusion)
    {
        if (protection is not null || exclusion is not null)
        {
            return RecommendationKind.Excluded;
        }

        if (topology == BranchTopology.Merged && activity == ActivityStatus.Inactive)
        {
            return RecommendationKind.CleanupCandidate;
        }

        if (activity == ActivityStatus.Inactive || topology is BranchTopology.Diverged
            or BranchTopology.Unrelated)
        {
            return RecommendationKind.Review;
        }

        return RecommendationKind.Keep;
    }

    private static string Explain(
        BranchTopology topology,
        ActivityStatus activity,
        string? protection,
        string? exclusion)
    {
        if (protection is not null)
        {
            return $"Protégée par le motif « {protection} »";
        }

        if (exclusion is not null)
        {
            return $"Exclue par le motif « {exclusion} »";
        }

        return (topology, activity) switch
        {
            (BranchTopology.Merged, ActivityStatus.Inactive) =>
                "Fusionnée et inactive : candidate au nettoyage manuel",
            (_, ActivityStatus.Inactive) =>
                "Inactive avec des faits Git à examiner",
            (BranchTopology.Diverged, _) => "Historique divergent à examiner",
            (BranchTopology.Unrelated, _) => "Aucun ancêtre commun avec la référence",
            _ => "Aucune action recommandée",
        };
    }

    private static string? Match(string reference, IReadOnlyList<string> patterns)
    {
        return patterns.FirstOrDefault(pattern =>
            FileSystemName.MatchesSimpleExpression(pattern, reference, ignoreCase: false));
    }
}
