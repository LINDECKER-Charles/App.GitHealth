namespace App.GitHealth.Api.Features.Assistant.Agents;

/// <summary>Why a run could not produce an answer.</summary>
internal enum AgentFailureCode
{
    Unavailable,
    TimedOut,
    Failed,
}

/// <summary>Everything the process left behind once it exited.</summary>
internal sealed record AgentRunOutcome
{
    public required int ExitCode { get; init; }

    public required string StandardOutput { get; init; }

    public required string StandardError { get; init; }

    /// <summary>The output budget was reached and the process was stopped short.</summary>
    public bool IsTruncated { get; init; }

    public bool IsSuccess => ExitCode == 0;
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
