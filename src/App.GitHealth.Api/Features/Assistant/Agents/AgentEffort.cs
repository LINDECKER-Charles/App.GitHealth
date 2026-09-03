namespace App.GitHealth.Api.Features.Assistant.Agents;

/// <summary>
/// How hard the agent is asked to think. Both supported CLIs happen to name their levels
/// identically, so the vocabulary is shared rather than mapped — a level shown in the
/// interface is the level the CLI receives, with nothing lost in translation.
/// </summary>
internal static class AgentEffort
{
    public const string Low = "low";
    public const string Medium = "medium";
    public const string High = "high";
    public const string ExtraHigh = "xhigh";
    public const string Maximum = "max";

    /// <summary>
    /// The levels offered, cheapest first. This list is also an allowlist: a level absent
    /// from it never reaches a command line, which is what keeps a request from writing its
    /// own arguments.
    /// </summary>
    public static IReadOnlyList<string> All { get; } = [Low, Medium, High, ExtraHigh, Maximum];

    /// <summary>Resolves a requested level, falling back to the agent's default.</summary>
    public static string Resolve(string? requested, AgentDefinition agent)
    {
        ArgumentNullException.ThrowIfNull(agent);
        var trimmed = requested?.Trim();
        return string.IsNullOrEmpty(trimmed) ? agent.DefaultEffort : trimmed;
    }

    public static bool IsSupported(string? effort, AgentDefinition agent)
    {
        ArgumentNullException.ThrowIfNull(agent);
        return agent.Efforts.Contains(effort, StringComparer.Ordinal);
    }
}
