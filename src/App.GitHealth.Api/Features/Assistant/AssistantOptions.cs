namespace App.GitHealth.Api.Features.Assistant;

/// <summary>Overrides applying to one agent of the catalog.</summary>
public sealed class AssistantAgentOptions
{
    /// <summary>
    /// Explicit path of the CLI. When empty, resolution falls back to the <c>PATH</c> and
    /// to the standard installation directories.
    /// </summary>
    public string? ExecutablePath { get; init; }
}

/// <summary>
/// Settings of the local agent assistant. It is the one feature of GitHealth that reaches a
/// network, so it is also the one with a switch that turns it off outright.
/// </summary>
public sealed class AssistantOptions
{
    public const string SectionName = "GitHealth:Assistant";
    public const int MinimumTimeoutSeconds = 10;
    public const int MaximumTimeoutSeconds = 900;
    public const int MinimumOutputBytes = 4 * 1024;
    public const int MaximumOutputBytesLimit = 8 * 1024 * 1024;
    public const int MinimumBranches = 1;
    public const int MaximumBranchesLimit = 2000;
    public const int MaximumParallelRunsLimit = 4;

    /// <summary>
    /// Turns the feature off for the whole installation. An administrator who does not want
    /// branch names leaving the machine sets this once, and no interface can re-enable it.
    /// </summary>
    public bool Enabled { get; init; } = true;

    public TimeSpan RunTimeout { get; init; } = TimeSpan.FromMinutes(3);

    /// <summary>
    /// Ceiling on what one run may write before it is stopped. The agents narrate themselves
    /// as JSON, so this covers a whole exchange — every event, and the capture rows a tool
    /// call sends back through it — rather than an answer.
    /// </summary>
    public int MaximumOutputBytes { get; init; } = 4 * 1024 * 1024;

    /// <summary>
    /// Branches carried by a briefing. Past this, rows are dropped and their number is
    /// stated in the text — a truncated table that says so beats a prompt too large to send.
    /// </summary>
    public int MaximumBranches { get; init; } = 200;

    /// <summary>
    /// Runs allowed at the same time. One by default: these calls are billed to the user's
    /// own account, and a queue that fans out spends their money without being asked.
    /// </summary>
    public int MaximumParallelRuns { get; init; } = 1;

    public Dictionary<string, AssistantAgentOptions> Agents { get; init; } =
        new(StringComparer.OrdinalIgnoreCase);

    public string? ExecutablePathFor(string agentId) =>
        Agents.TryGetValue(agentId, out var agent) ? agent.ExecutablePath : null;
}
