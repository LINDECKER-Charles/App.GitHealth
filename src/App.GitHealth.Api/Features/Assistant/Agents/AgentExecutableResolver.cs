using Microsoft.Extensions.Options;

namespace App.GitHealth.Api.Features.Assistant.Agents;

/// <summary>
/// Locates the agent CLIs installed on the machine. Resolution is performed once: the set
/// of installed tools does not change while a session is open, and re-walking the file
/// system on every request would buy nothing.
/// </summary>
internal sealed class AgentExecutableResolver
{
    private readonly AssistantOptions _options;
    private readonly AgentExecutableSearch _search;
    private readonly Lazy<IReadOnlyList<AgentLocation>> _locations;

    public AgentExecutableResolver(AssistantOptions options, AgentExecutableSearch search)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(search);
        _options = options;
        _search = search;
        _locations = new Lazy<IReadOnlyList<AgentLocation>>(
            Locate,
            LazyThreadSafetyMode.ExecutionAndPublication);
    }

    public IReadOnlyList<AgentLocation> Locations => _locations.Value;

    public static AgentExecutableResolver Capture(IOptions<AssistantOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);
        return new AgentExecutableResolver(options.Value, AgentExecutableSearch.Capture());
    }

    public AgentLocation? Find(string? agentId) => Locations.FirstOrDefault(
        location => string.Equals(location.Agent.Id, agentId, StringComparison.OrdinalIgnoreCase));

    private IReadOnlyList<AgentLocation> Locate() =>
        [.. AgentCatalog.All.Select(LocateAgent)];

    private AgentLocation LocateAgent(AgentDefinition agent)
    {
        var directories = _search.DirectoriesFor(agent).ToArray();
        return new AgentLocation
        {
            Agent = agent,
            ExecutablePath = FirstExisting(Configured(agent))
                ?? FirstExisting(Candidates(agent, _search.PathDirectories))
                ?? FirstExisting(Candidates(agent, directories)),
            SearchedDirectories = directories,
        };
    }

    private string? FirstExisting(IEnumerable<string> candidates) =>
        candidates.FirstOrDefault(_search.FileExists);

    private IEnumerable<string> Configured(AgentDefinition agent)
    {
        var configured = _options.ExecutablePathFor(agent.Id);
        return string.IsNullOrWhiteSpace(configured) ? [] : [configured.Trim()];
    }

    private IEnumerable<string> Candidates(
        AgentDefinition agent,
        IReadOnlyList<string> directories) =>
        from directory in directories
        from suffix in _search.ExecutableSuffixes
        select Path.Combine(directory, agent.Id + suffix);
}
