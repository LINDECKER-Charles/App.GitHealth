using App.GitHealth.Api.Features.Assistant.Agents;
using Microsoft.Extensions.Options;

namespace App.GitHealth.Api.Features.Assistant;

/// <summary>
/// Says which agents are actually usable. Finding the file is not enough — a CLI that is
/// present but broken must read as unavailable, so each one is asked for its version and
/// has to answer.
/// </summary>
/// <remarks>
/// The probe is deliberately not run at startup, unlike Git's. An agent installed while
/// GitHealth is open should appear without a restart, and the cost of the check belongs to
/// the screen that needs it rather than to every launch.
/// </remarks>
internal sealed class AgentAvailabilityService(
    AgentExecutableResolver resolver,
    IOptions<AssistantOptions> options) : IDisposable
{
    private static readonly TimeSpan ProbeTimeout = TimeSpan.FromSeconds(20);
    private const int ProbeOutputBytes = 8 * 1024;

    private readonly SemaphoreSlim _gate = new(1, 1);
    private IReadOnlyList<AgentLocation>? _probed;

    public bool IsEnabled => options.Value.Enabled;

    /// <summary>
    /// Reads the catalog with each agent's version filled in. The result is kept for the
    /// session; <paramref name="refresh" /> is what the interface's own refresh maps to.
    /// </summary>
    public async Task<IReadOnlyList<AgentLocation>> ReadAsync(
        bool refresh,
        CancellationToken cancellationToken)
    {
        if (!refresh && _probed is { } cached)
        {
            return cached;
        }

        await _gate.WaitAsync(cancellationToken);
        try
        {
            if (!refresh && _probed is { } raced)
            {
                return raced;
            }

            _probed = await ProbeAllAsync(cancellationToken);
            return _probed;
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>Reads one agent, refusing the ones that never answered their version.</summary>
    public async Task<AgentLocation?> FindAvailableAsync(
        string? agentId,
        CancellationToken cancellationToken)
    {
        var agents = await ReadAsync(refresh: false, cancellationToken);
        var location = agents.FirstOrDefault(agent => string.Equals(
            agent.Agent.Id,
            agentId,
            StringComparison.OrdinalIgnoreCase));
        return location?.Version is null ? null : location;
    }

    private async Task<IReadOnlyList<AgentLocation>> ProbeAllAsync(
        CancellationToken cancellationToken)
    {
        var probes = resolver.Locations.Select(location => ProbeAsync(location, cancellationToken));
        return await Task.WhenAll(probes);
    }

    private static async Task<AgentLocation> ProbeAsync(
        AgentLocation location,
        CancellationToken cancellationToken)
    {
        if (!location.IsResolved)
        {
            return location;
        }

        try
        {
            var outcome = await AgentProcessRunner.RunAsync(
                CreateProbe(location),
                trace: null,
                cancellationToken);
            return outcome.IsSuccess
                ? location with { Version = FirstLine(outcome.StandardOutput) }
                : location;
        }
        catch (Exception exception) when (exception is AgentProcessException or IOException)
        {
            // Present but unusable reads exactly like absent, which is what it is here.
            return location;
        }
    }

    private static AgentRunRequest CreateProbe(AgentLocation location) => new()
    {
        CommandLine = AgentCommandLine.ForVersion(location),
        WorkingDirectory = Path.GetTempPath(),
        Prompt = string.Empty,
        Timeout = ProbeTimeout,
        MaximumOutputBytes = ProbeOutputBytes,
    };

    public void Dispose() => _gate.Dispose();

    private static string? FirstLine(string output)
    {
        var line = output
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .FirstOrDefault();
        return string.IsNullOrWhiteSpace(line) ? null : line;
    }
}
