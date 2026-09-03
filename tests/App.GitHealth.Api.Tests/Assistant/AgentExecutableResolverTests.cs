using App.GitHealth.Api.Features.Assistant;
using App.GitHealth.Api.Features.Assistant.Agents;

namespace App.GitHealth.Api.Tests.Assistant;

public sealed class AgentExecutableResolverTests
{
    private const string AgentId = "claude";
    private const string ConfiguredPath = "/opt/tooling/claude";
    private static readonly string PathCandidate = Path.Combine("/usr/bin", AgentId);
    private static readonly string StandardCandidate = Path.Combine("/opt/homebrew/bin", AgentId);

    [Fact]
    public void ConfiguredPathWinsOverThePathAndTheStandardDirectories()
    {
        var resolver = Resolve(ConfiguredPath, ConfiguredPath, PathCandidate, StandardCandidate);

        Assert.Equal(ConfiguredPath, Claude(resolver).ExecutablePath);
    }

    [Fact]
    public void ThePathIsUsedWhenTheConfiguredExecutableIsMissing()
    {
        var resolver = Resolve(ConfiguredPath, PathCandidate, StandardCandidate);

        Assert.Equal(PathCandidate, Claude(resolver).ExecutablePath);
    }

    /// <summary>
    /// The case that matters most on a desktop: launched from the Finder, the process gets
    /// the system's minimal PATH, and the CLI the user installed is only reachable through
    /// the standard directories.
    /// </summary>
    [Fact]
    public void StandardDirectoriesAreUsedWhenThePathDoesNotCarryTheAgent()
    {
        var resolver = Resolve(configuredPath: null, StandardCandidate);

        Assert.Equal(StandardCandidate, Claude(resolver).ExecutablePath);
    }

    [Fact]
    public void EveryCatalogAgentIsResolvedIndependently()
    {
        var resolver = Resolve(configuredPath: null, StandardCandidate);

        Assert.Equal(AgentCatalog.All.Count, resolver.Locations.Count);
        Assert.True(Claude(resolver).IsResolved);
        Assert.False(resolver.Find("codex")!.IsResolved);
    }

    [Fact]
    public void AMissingAgentProducesADiagnosticSayingWhereToLookAndWhatToSet()
    {
        var location = Claude(Resolve(configuredPath: null));

        Assert.False(location.IsResolved);
        Assert.Contains("Claude Code was not found", location.UnavailableMessage, Ordinal);
        Assert.Contains("/opt/homebrew/bin", location.UnavailableMessage, Ordinal);
        Assert.Contains(
            "GitHealth:Assistant:Agents:claude:ExecutablePath",
            location.UnavailableMessage,
            Ordinal);
    }

    [Fact]
    public void AnAgentOutsideTheCatalogIsNeverResolved()
    {
        var resolver = Resolve(configuredPath: null, StandardCandidate);

        Assert.Null(resolver.Find("bash"));
    }

    private const StringComparison Ordinal = StringComparison.Ordinal;

    private static AgentLocation Claude(AgentExecutableResolver resolver) =>
        resolver.Find(AgentId)!;

    private static AgentExecutableResolver Resolve(
        string? configuredPath,
        params string[] existing)
    {
        var options = new AssistantOptions();
        if (configuredPath is not null)
        {
            options.Agents[AgentId] = new AssistantAgentOptions
            {
                ExecutablePath = configuredPath,
            };
        }

        return new AgentExecutableResolver(options, new AgentExecutableSearch
        {
            ExecutableSuffixes = [string.Empty],
            PathDirectories = ["/usr/bin"],
            StandardDirectories = ["/opt/homebrew/bin"],
            FileExists = existing.Contains,
        });
    }
}
