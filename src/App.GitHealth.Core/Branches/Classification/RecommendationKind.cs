namespace App.GitHealth.Core.Branches;

public enum RecommendationKind
{
    Keep,
    Review,
    CleanupCandidate,
    Excluded,

    /// <summary>
    /// The branch has no own commits and the deadline is still running. This is not
    /// "keep": there is nothing to preserve, only nothing to do right now.
    /// </summary>
    Merged,
}
