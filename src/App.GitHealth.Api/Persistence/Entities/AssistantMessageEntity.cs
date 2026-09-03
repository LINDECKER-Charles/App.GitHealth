using App.GitHealth.Api.Persistence.Models.Assistant;

namespace App.GitHealth.Api.Persistence.Entities;

/// <summary>
/// One turn of a stored conversation. A question and an answer are the same row shape so
/// that reading a thread back is a single ordered query rather than two interleaved ones.
/// </summary>
internal sealed class AssistantMessageEntity
{
    public const string UserRole = "user";
    public const string AgentRole = "agent";

    private AssistantMessageEntity()
    {
    }

    public Guid Id { get; private set; }

    public Guid ConversationId { get; private set; }

    public AssistantConversationEntity Conversation { get; private set; } = null!;

    /// <summary>Order within the thread. Timestamps collide; positions do not.</summary>
    public int Position { get; private set; }

    public string Role { get; private set; } = UserRole;

    public string Text { get; private set; } = string.Empty;

    public DateTimeOffset WrittenAtUtc { get; private set; }

    /// <summary>How an agent turn ended. Null on a question, which cannot fail.</summary>
    public string? Status { get; private set; }

    public string? Effort { get; private set; }

    /// <summary>The command that produced the answer, with its bridge token blanked.</summary>
    public string? CommandLine { get; private set; }

    public string? FailureCode { get; private set; }

    public string? FailureMessage { get; private set; }

    public int? DurationMs { get; private set; }

    public bool IsTruncated { get; private set; }

    public static AssistantMessageEntity Asked(AssistantTurnRecord turn, int position)
    {
        ArgumentNullException.ThrowIfNull(turn);
        return new AssistantMessageEntity
        {
            Id = Guid.NewGuid(),
            Position = position,
            Role = UserRole,
            Text = turn.Question,
            WrittenAtUtc = turn.AskedAtUtc,
        };
    }

    public static AssistantMessageEntity Answered(AssistantTurnRecord turn, int position)
    {
        ArgumentNullException.ThrowIfNull(turn);
        return new AssistantMessageEntity
        {
            Id = Guid.NewGuid(),
            Position = position,
            Role = AgentRole,
            Text = turn.Answer ?? string.Empty,
            WrittenAtUtc = turn.SettledAtUtc,
            Status = turn.Status,
            Effort = turn.Effort,
            CommandLine = turn.CommandLine,
            FailureCode = turn.FailureCode,
            FailureMessage = turn.FailureMessage,
            DurationMs = Duration(turn),
            IsTruncated = turn.IsTruncated,
        };
    }

    private static int? Duration(AssistantTurnRecord turn)
    {
        var elapsed = turn.SettledAtUtc - turn.AskedAtUtc;
        return elapsed < TimeSpan.Zero ? null : (int)elapsed.TotalMilliseconds;
    }
}
