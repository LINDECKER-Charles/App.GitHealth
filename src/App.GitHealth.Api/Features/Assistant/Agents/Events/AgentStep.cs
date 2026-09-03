namespace App.GitHealth.Api.Features.Assistant.Agents.Events;

/// <summary>
/// What an agent is busy with, in the few words both supported CLIs can be read into. The
/// vocabulary is deliberately small: it has to be phrased in the interface's own language,
/// and a step nobody can name is a step nobody can show.
/// </summary>
internal enum AgentStepKind
{
    /// <summary>The turn is with the model and nothing has come back from it yet.</summary>
    Waiting,

    /// <summary>The model is reasoning. Claude Code keeps the text of it to itself.</summary>
    Thinking,

    /// <summary>A capture tool was called, which is the only thing a run can do.</summary>
    Tool,

    /// <summary>Prose is being written — a note about what comes next, or the answer.</summary>
    Writing,
}

/// <summary>
/// One thing the agent did, the moment it started doing it. Steps are never written to the
/// history: they are worth watching while a run happens and worth nothing once it has.
/// </summary>
/// <param name="Kind">What the agent is doing.</param>
/// <param name="Label">The tool that was called. Empty for every other kind.</param>
/// <param name="Detail">What the call asked for, short enough to read at a glance.</param>
internal sealed record AgentStep(AgentStepKind Kind, string Label = "", string? Detail = null);
