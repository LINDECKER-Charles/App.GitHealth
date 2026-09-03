namespace App.GitHealth.Api.Persistence.Models.Assistant;

/// <summary>
/// One settled exchange, written in a single step. A turn is only recorded once the agent
/// has stopped: an answer half-written is worth reading in the panel, not worth keeping.
/// </summary>
internal sealed record AssistantTurnRecord
{
    /// <summary>The thread to append to, created on the first turn that names it.</summary>
    public required Guid ConversationId { get; init; }

    /// <summary>The capture that was read. The conversation dies with it.</summary>
    public required Guid AnalysisRunId { get; init; }

    public required string AgentId { get; init; }

    public required string AgentName { get; init; }

    public required string Effort { get; init; }

    public required string CommandLine { get; init; }

    public required int BranchCount { get; init; }

    public required string Question { get; init; }

    public required DateTimeOffset AskedAtUtc { get; init; }

    public required DateTimeOffset SettledAtUtc { get; init; }

    /// <summary>Completed, Failed or Cancelled, as the run reported it.</summary>
    public required string Status { get; init; }

    public string? Answer { get; init; }

    public string? FailureCode { get; init; }

    public string? FailureMessage { get; init; }

    public bool IsTruncated { get; init; }
}
