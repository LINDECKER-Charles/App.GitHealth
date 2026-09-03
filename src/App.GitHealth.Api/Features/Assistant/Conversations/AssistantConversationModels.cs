using App.GitHealth.Api.Persistence.Entities;
using App.GitHealth.Api.Persistence.Models.Assistant;

namespace App.GitHealth.Api.Features.Assistant.Conversations;

/// <summary>One line of the conversation list, across every baseline of a repository.</summary>
internal sealed record AssistantConversationSummaryResponse
{
    public required Guid Id { get; init; }

    public required Guid AnalysisId { get; init; }

    public required string Baseline { get; init; }

    public required string AgentId { get; init; }

    public required string AgentName { get; init; }

    public required string Title { get; init; }

    public required int AnswerCount { get; init; }

    public required DateTimeOffset StartedAtUtc { get; init; }

    public required DateTimeOffset UpdatedAtUtc { get; init; }

    public static AssistantConversationSummaryResponse From(AssistantConversationSummary row)
    {
        ArgumentNullException.ThrowIfNull(row);
        return new AssistantConversationSummaryResponse
        {
            Id = row.Id,
            AnalysisId = row.AnalysisRunId,
            Baseline = row.Baseline,
            AgentId = row.AgentId,
            AgentName = row.AgentName,
            Title = row.Title,
            AnswerCount = row.AnswerCount,
            StartedAtUtc = row.StartedAtUtc,
            UpdatedAtUtc = row.UpdatedAtUtc,
        };
    }
}

internal sealed record AssistantConversationListResponse
{
    public required IReadOnlyList<AssistantConversationSummaryResponse> Conversations
    {
        get;
        init;
    }
}

/// <summary>One stored turn, question or answer, in the order it was written.</summary>
internal sealed record AssistantMessageResponse
{
    public required Guid Id { get; init; }

    public required int Position { get; init; }

    /// <summary><c>user</c> or <c>agent</c>.</summary>
    public required string Role { get; init; }

    public required string Text { get; init; }

    public required DateTimeOffset WrittenAtUtc { get; init; }

    public string? Status { get; init; }

    public string? Effort { get; init; }

    public string? CommandLine { get; init; }

    public string? FailureCode { get; init; }

    public string? FailureMessage { get; init; }

    public int? DurationMs { get; init; }

    public bool IsTruncated { get; init; }

    public static AssistantMessageResponse From(AssistantMessageEntity message)
    {
        ArgumentNullException.ThrowIfNull(message);
        return new AssistantMessageResponse
        {
            Id = message.Id,
            Position = message.Position,
            Role = message.Role,
            Text = message.Text,
            WrittenAtUtc = message.WrittenAtUtc,
            Status = message.Status,
            Effort = message.Effort,
            CommandLine = message.CommandLine,
            FailureCode = message.FailureCode,
            FailureMessage = message.FailureMessage,
            DurationMs = message.DurationMs,
            IsTruncated = message.IsTruncated,
        };
    }
}

internal sealed record AssistantConversationResponse
{
    public required Guid Id { get; init; }

    public required Guid AnalysisId { get; init; }

    public required string Baseline { get; init; }

    public required string AgentId { get; init; }

    public required string AgentName { get; init; }

    public required string Title { get; init; }

    /// <summary>Rows the agent could read when the thread was written.</summary>
    public required int BranchCount { get; init; }

    public required DateTimeOffset StartedAtUtc { get; init; }

    public required DateTimeOffset UpdatedAtUtc { get; init; }

    public required IReadOnlyList<AssistantMessageResponse> Messages { get; init; }

    public static AssistantConversationResponse From(AssistantConversationEntity thread)
    {
        ArgumentNullException.ThrowIfNull(thread);
        return new AssistantConversationResponse
        {
            Id = thread.Id,
            AnalysisId = thread.AnalysisRunId,
            Baseline = thread.AnalysisRun.ReferenceName,
            AgentId = thread.AgentId,
            AgentName = thread.AgentName,
            Title = thread.Title,
            BranchCount = thread.BranchCount,
            StartedAtUtc = thread.StartedAtUtc,
            UpdatedAtUtc = thread.UpdatedAtUtc,
            Messages = [.. thread.Messages
                .OrderBy(message => message.Position)
                .Select(AssistantMessageResponse.From)],
        };
    }
}

/// <summary>What the panel and the policy screen both need to know before showing anything.</summary>
internal sealed record AssistantStatusResponse
{
    /// <summary>Null while sending this repository's captures has never been allowed.</summary>
    public required DateTimeOffset? ConsentGrantedAtUtc { get; init; }

    public required int ConversationCount { get; init; }
}

/// <summary>Grants or withdraws the permission, from either screen that offers it.</summary>
internal sealed record AssistantConsentRequest
{
    public bool Granted { get; init; }
}

internal sealed record AssistantPurgeResponse
{
    public required int Deleted { get; init; }
}
