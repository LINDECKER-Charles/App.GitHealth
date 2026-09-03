namespace App.GitHealth.Api.Persistence.Models.Assistant;

/// <summary>
/// One line of the conversation list. It carries the baseline of the capture it read rather
/// than its identifier: the list is shown across every baseline of a repository, and the
/// baseline is what tells two otherwise identical threads apart.
/// </summary>
internal sealed record AssistantConversationSummary
{
    public required Guid Id { get; init; }

    public required Guid AnalysisRunId { get; init; }

    public required string Baseline { get; init; }

    public required string AgentId { get; init; }

    public required string AgentName { get; init; }

    public required string Title { get; init; }

    /// <summary>Answers in the thread, which is half its messages.</summary>
    public required int AnswerCount { get; init; }

    public required DateTimeOffset StartedAtUtc { get; init; }

    public required DateTimeOffset UpdatedAtUtc { get; init; }
}
