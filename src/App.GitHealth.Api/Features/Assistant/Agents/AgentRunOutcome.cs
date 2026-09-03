namespace App.GitHealth.Api.Features.Assistant.Agents;

/// <summary>Why a run could not produce an answer.</summary>
internal enum AgentFailureCode
{
    Unavailable,
    TimedOut,
    Failed,
}

/// <summary>
/// What the process left behind once it exited. Its standard output is not here: it is read
/// as it arrives by whoever asked for the run, and what is worth keeping of it is theirs.
/// </summary>
internal sealed record AgentRunOutcome
{
    public required int ExitCode { get; init; }

    public required string StandardError { get; init; }

    /// <summary>The output budget was reached and the process was stopped short.</summary>
    public bool IsTruncated { get; init; }

    public bool IsSuccess => ExitCode == 0;
}

/// <summary>What one run needs before its command line can be written out.</summary>
internal sealed record AgentRunOptions
{
    /// <summary>Where an agent that reports through a file is told to write its answer.</summary>
    public required string AnswerFilePath { get; init; }

    /// <summary>An allowlisted level, never a value taken straight from the request.</summary>
    public required string Effort { get; init; }

    /// <summary>Where this run reaches the capture. Single-use, and dead once it settles.</summary>
    public required Uri BridgeAddress { get; init; }
}

/// <summary>What GitHealth is about to run, and with what.</summary>
internal sealed record AgentRunRequest
{
    public required AgentCommandLine CommandLine { get; init; }

    /// <summary>An empty scratch directory: the agent is given nothing to read on disk.</summary>
    public required string WorkingDirectory { get; init; }

    public required string Prompt { get; init; }

    public required TimeSpan Timeout { get; init; }

    public required int MaximumOutputBytes { get; init; }
}

internal sealed class AgentProcessException(AgentFailureCode code, string message)
    : Exception(message)
{
    public AgentFailureCode Code { get; } = code;
}
