using App.GitHealth.Api.Persistence.Models.Assistant;

namespace App.GitHealth.Api.Persistence.Entities;

/// <summary>
/// One thread of questions and answers about one capture. It hangs off the analysis run it
/// read, not off the project: a conversation is only meaningful next to the measurements it
/// argued about, so deleting that capture takes the conversation with it.
/// </summary>
internal sealed class AssistantConversationEntity
{
    /// <summary>A title is a first question, cut where a list of them stays readable.</summary>
    public const int MaximumTitleLength = 300;

    private AssistantConversationEntity()
    {
    }

    public Guid Id { get; private set; }

    public Guid AnalysisRunId { get; private set; }

    public AnalysisRunEntity AnalysisRun { get; private set; } = null!;

    public string AgentId { get; private set; } = string.Empty;

    public string AgentName { get; private set; } = string.Empty;

    public string Title { get; private set; } = string.Empty;

    /// <summary>Rows the agent could read, so a stored answer keeps the scale it was given.</summary>
    public int BranchCount { get; private set; }

    public DateTimeOffset StartedAtUtc { get; private set; }

    public DateTimeOffset UpdatedAtUtc { get; private set; }

    public ICollection<AssistantMessageEntity> Messages { get; } = [];

    public static AssistantConversationEntity Open(AssistantTurnRecord turn)
    {
        ArgumentNullException.ThrowIfNull(turn);
        var conversation = new AssistantConversationEntity
        {
            Id = turn.ConversationId,
            AnalysisRunId = turn.AnalysisRunId,
            AgentId = turn.AgentId,
            AgentName = turn.AgentName,
            Title = Shorten(turn.Question),
            BranchCount = turn.BranchCount,
            StartedAtUtc = turn.AskedAtUtc,
            UpdatedAtUtc = turn.SettledAtUtc,
        };
        conversation.Append(turn);
        return conversation;
    }

    /// <summary>
    /// Adds a turn to a thread already open. The agent is re-read from the turn because a
    /// follow-up may well be asked of a different one than the question before it.
    /// </summary>
    public void Append(AssistantTurnRecord turn)
    {
        ArgumentNullException.ThrowIfNull(turn);
        AgentId = turn.AgentId;
        AgentName = turn.AgentName;
        UpdatedAtUtc = turn.SettledAtUtc;
        Messages.Add(AssistantMessageEntity.Asked(turn, Messages.Count));
        Messages.Add(AssistantMessageEntity.Answered(turn, Messages.Count));
    }

    private static string Shorten(string question) =>
        question.Length <= MaximumTitleLength
            ? question
            : question[..MaximumTitleLength].TrimEnd();
}
