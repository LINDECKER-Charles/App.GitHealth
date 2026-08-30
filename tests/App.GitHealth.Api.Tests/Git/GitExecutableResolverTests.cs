using App.GitHealth.Api.Git.Process;

namespace App.GitHealth.Api.Tests.Git;

public sealed class GitExecutableResolverTests
{
    private const string ConfiguredPath = "/opt/outillage/git";
    private const string PathDirectory = "/usr/bin";
    private const string ExecutableName = "git";
    private static readonly string PathCandidate = Path.Combine(PathDirectory, ExecutableName);
    private static readonly string StandardLocation = Path.Combine(
        "/opt",
        "homebrew",
        "bin",
        ExecutableName);

    [Fact]
    public void ConfiguredPathWinsOverThePathAndStandardLocations()
    {
        var resolver = Resolve(ConfiguredPath, ConfiguredPath, PathCandidate, StandardLocation);

        Assert.True(resolver.Location.IsResolved);
        Assert.Equal(ConfiguredPath, resolver.Location.ExecutablePath);
    }

    [Fact]
    public void PathIsUsedWhenTheConfiguredExecutableIsMissing()
    {
        var resolver = Resolve(ConfiguredPath, PathCandidate, StandardLocation);

        Assert.Equal(PathCandidate, resolver.Location.ExecutablePath);
    }

    [Fact]
    public void StandardLocationsAreUsedWhenGitIsAbsentFromThePath()
    {
        var resolver = Resolve(configuredPath: null, StandardLocation);

        Assert.Equal(StandardLocation, resolver.Location.ExecutablePath);
    }

    [Fact]
    public void MissingGitProducesAnActionableDiagnostic()
    {
        var resolver = Resolve(ConfiguredPath);

        var location = resolver.Location;

        Assert.False(location.IsResolved);
        Assert.Null(location.ExecutablePath);
        Assert.Contains(ConfiguredPath, location.SearchedLocations);
        Assert.Contains(StandardLocation, location.SearchedLocations);
        Assert.Contains("--git-path", location.UnavailableMessage, StringComparison.Ordinal);
        Assert.Contains("PATH", location.UnavailableMessage, StringComparison.Ordinal);
        Assert.Contains(StandardLocation, location.UnavailableMessage, StringComparison.Ordinal);
    }

    [Fact]
    public void BlankConfiguredPathIsIgnored()
    {
        var resolver = Resolve("   ", PathCandidate);

        Assert.Equal(PathCandidate, resolver.Location.ExecutablePath);
        Assert.DoesNotContain("   ", resolver.Location.SearchedLocations);
    }

    private static GitExecutableResolver Resolve(
        string? configuredPath,
        params string[] existingFiles)
    {
        var existing = new HashSet<string>(existingFiles, StringComparer.Ordinal);
        return new GitExecutableResolver(configuredPath, new GitExecutableSearch
        {
            ExecutableNames = [ExecutableName],
            PathDirectories = [PathDirectory],
            StandardLocations = [StandardLocation],
            FileExists = existing.Contains,
        });
    }
}
