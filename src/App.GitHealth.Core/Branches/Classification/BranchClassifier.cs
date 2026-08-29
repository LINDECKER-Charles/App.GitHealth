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
    /// Une branche sans commit propre est mesurée sur l'échelle réduite : la référence
    /// contient déjà tout son historique, donc rien ne se perd à la supprimer.
    /// </summary>
    public static ActivityThresholds AppliedThresholds(
        BranchTopology topology,
        ActivityThresholds thresholds)
    {
        ArgumentNullException.ThrowIfNull(thresholds);
        return HasOwnCommits(topology) ? thresholds : thresholds.ShortenTo(ActivityThresholds.Merged);
    }

    /// <summary>
    /// Faux quand tous les commits de la branche sont déjà accessibles depuis la
    /// référence : sommet identique, ou branche strictement ancêtre.
    /// </summary>
    public static bool HasOwnCommits(BranchTopology topology)
    {
        return topology is not (BranchTopology.Merged or BranchTopology.Synchronized);
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
            return $"Protégée par le motif « {assessment.Protection} »";
        }

        if (assessment.Exclusion is not null)
        {
            return $"Exclue par le motif « {assessment.Exclusion} »";
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
                "Aucun commit propre et sans activité depuis longtemps : "
                + "candidate au nettoyage manuel",
            ActivityStatus.Aging =>
                "Aucun commit propre : la référence contient déjà tout son historique",
            ActivityStatus.Active =>
                "Terminée : la référence contient déjà tout, le délai court encore",
            _ => "Aucun commit propre, sans date de sommet exploitable",
        };
    }

    private static string ExplainOwnCommits(Assessment assessment)
    {
        return (assessment.Topology, assessment.Activity) switch
        {
            (_, ActivityStatus.Inactive) => "Inactive avec des faits Git à examiner",
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
