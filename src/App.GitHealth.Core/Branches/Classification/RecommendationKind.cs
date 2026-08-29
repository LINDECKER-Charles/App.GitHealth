namespace App.GitHealth.Core.Branches;

public enum RecommendationKind
{
    Keep,
    Review,
    CleanupCandidate,
    Excluded,

    /// <summary>
    /// La branche n'a aucun commit propre et le délai court encore. Ce n'est pas
    /// « conserver » : il n'y a rien à préserver, seulement rien à faire dans l'immédiat.
    /// </summary>
    Merged,
}
