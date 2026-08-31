namespace App.GitHealth.Api.Git.Process;

/// <summary>
/// Locates Git without depending on the <c>PATH</c>: configured path, then <c>PATH</c>, then
/// the platform's standard installation locations. The first one found wins.
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
    /// Resolution is performed only once: the machine does not change during a session.
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
