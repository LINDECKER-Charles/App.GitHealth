namespace App.GitHealth.Api.Git.Process;

/// <summary>
/// Localise Git sans dépendre du <c>PATH</c> : chemin configuré, puis <c>PATH</c>, puis
/// emplacements d'installation standards de la plateforme. Le premier trouvé gagne.
/// </summary>
internal sealed class GitExecutableResolver
{
    private readonly string? _configuredPath;
    private readonly GitExecutableSearch _search;
    private readonly Lazy<GitExecutableLocation> _location;

    public GitExecutableResolver(string? configuredPath, GitExecutableSearch search)
    {
        ArgumentNullException.ThrowIfNull(search);
        _configuredPath = string.IsNullOrWhiteSpace(configuredPath) ? null : configuredPath.Trim();
        _search = search;
        _location = new Lazy<GitExecutableLocation>(
            Locate,
            LazyThreadSafetyMode.ExecutionAndPublication);
    }

    /// <summary>
    /// Résolution effectuée une seule fois : le poste ne change pas en cours de session.
    /// </summary>
    public GitExecutableLocation Location => _location.Value;

    public static GitExecutableResolver Capture(string? configuredPath) =>
        new(configuredPath, GitExecutableSearch.Capture());

    private GitExecutableLocation Locate() => new()
    {
        ExecutablePath = FirstExisting(ConfiguredCandidates())
            ?? FirstExisting(PathCandidates())
            ?? FirstExisting(_search.StandardLocations),
        SearchedLocations = [.. ConfiguredCandidates(), .. _search.StandardLocations],
    };

    private string? FirstExisting(IEnumerable<string> candidates) =>
        candidates.FirstOrDefault(_search.FileExists);

    private IEnumerable<string> ConfiguredCandidates() =>
        _configuredPath is null ? [] : [_configuredPath];

    private IEnumerable<string> PathCandidates() =>
        from directory in _search.PathDirectories
        from executableName in _search.ExecutableNames
        select Path.Combine(directory, executableName);
}
