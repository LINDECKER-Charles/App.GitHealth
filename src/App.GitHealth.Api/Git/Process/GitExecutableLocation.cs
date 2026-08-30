namespace App.GitHealth.Api.Git.Process;

/// <summary>
/// Résultat de la recherche de Git : le chemin retenu, et de quoi rendre son absence actionnable.
/// </summary>
internal sealed record GitExecutableLocation
{
    public const string ConfigurationKey = "GitHealth:Git:ExecutablePath";
    public const string CommandLineOption = "--git-path";

    /// <summary>Chemin retenu, ou <see langword="null" /> si aucun candidat n'existe.</summary>
    public string? ExecutablePath { get; init; }

    /// <summary>
    /// Emplacements explicites testés. Les entrées du <c>PATH</c> en sont exclues : trop
    /// nombreuses pour un diagnostic lisible, elles sont mentionnées collectivement.
    /// </summary>
    public required IReadOnlyList<string> SearchedLocations { get; init; }

    public bool IsResolved => ExecutablePath is not null;

    /// <summary>
    /// Message affiché quand Git reste introuvable : où l'on a cherché, et quoi faire.
    /// </summary>
    public string UnavailableMessage =>
        $"Git est introuvable. Emplacements testés : le PATH{DescribeSearchedLocations()}. "
        + $"Indiquez le chemin de l'exécutable avec {CommandLineOption} <chemin> "
        + $"ou la configuration {ConfigurationKey}.";

    private string DescribeSearchedLocations() => SearchedLocations.Count == 0
        ? string.Empty
        : $", {string.Join(", ", SearchedLocations)}";
}
