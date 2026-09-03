namespace App.GitHealth.Api.Features.Assistant.Agents.Events;

/// <summary>
/// What one line of an agent's stream meant. Most lines mean nothing to a reader — a
/// handshake, a token count, a rate-limit notice — and say so by carrying nothing.
/// </summary>
internal sealed record AgentEvent
{
    public static AgentEvent None { get; } = new();

    /// <summary>What the agent has just started doing. A message can start several tools.</summary>
    public IReadOnlyList<AgentStep> Steps { get; init; } = [];

    /// <summary>Prose the agent wrote, in the order it wrote it.</summary>
    public string? Written { get; init; }

    /// <summary>
    /// The answer as the agent reports it once it is done, which supersedes everything it
    /// wrote on the way there.
    /// </summary>
    public string? Answer { get; init; }

    public static AgentEvent Step(AgentStep step) => new() { Steps = [step] };
}
