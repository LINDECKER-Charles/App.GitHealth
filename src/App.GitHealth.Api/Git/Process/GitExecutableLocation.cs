namespace App.GitHealth.Api.Git.Process;

/// <summary>
/// Result of the search for Git: the path that was selected, and enough to make its absence
/// actionable.
/// </summary>
internal sealed record GitExecutableLocation
{
    public const string ConfigurationKey = "GitHealth:Git:ExecutablePath";
    public const string CommandLineOption = "--git-path";

    /// <summary>Path selected, or <see langword="null" /> if no candidate exists.</summary>
    public string? ExecutablePath { get; init; }

    /// <summary>
    /// Explicit locations tried. The <c>PATH</c> entries are left out: too numerous for a
    /// readable diagnostic, they are mentioned collectively.
    /// </summary>
    public required IReadOnlyList<string> SearchedLocations { get; init; }

    public bool IsResolved => ExecutablePath is not null;

    /// <summary>
    /// Message shown when Git cannot be found: where the search looked, and what to do.
    /// </summary>
    public string UnavailableMessage =>
        $"Git cannot be found. Locations tried: the PATH{DescribeSearchedLocations()}. "
        + $"Point at the executable with {CommandLineOption} <path> "
        + $"or the {ConfigurationKey} setting.";

    private string DescribeSearchedLocations() => SearchedLocations.Count == 0
        ? string.Empty
        : $", {string.Join(", ", SearchedLocations)}";
}
