using App.GitHealth.Api.Persistence.Models;
using App.GitHealth.Api.Persistence.Models.Assistant;
using App.GitHealth.Api.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;

namespace App.GitHealth.Api.Features.Assistant.Conversations;

/// <summary>
/// Writes a settled exchange to the history, and never lets that write break the run. The
/// answer is already on screen by the time this happens: failing to keep a copy of it is
/// worth a log line, not an error the reader has to deal with.
/// </summary>
internal sealed partial class AssistantTurnRecorder(
    IAssistantConversationRepository conversations,
    ILogger<AssistantTurnRecorder> logger)
{
    public async Task RecordAsync(AssistantTurnRecord turn)
    {
        ArgumentNullException.ThrowIfNull(turn);
        try
        {
            if (!await conversations.AppendAsync(turn, CancellationToken.None))
            {
                LogCaptureGone(logger, turn.ConversationId);
            }
        }
        catch (Exception exception) when (exception is PersistenceWriteException
            or DbUpdateException)
        {
            LogNotStored(logger, turn.ConversationId, exception);
        }
    }

    [LoggerMessage(
        EventId = 3001,
        Level = LogLevel.Information,
        Message = "The capture of conversation {ConversationId} was deleted while the agent"
            + " was answering, so the exchange was not kept.")]
    private static partial void LogCaptureGone(ILogger logger, Guid conversationId);

    [LoggerMessage(
        EventId = 3002,
        Level = LogLevel.Warning,
        Message = "The exchange of conversation {ConversationId} could not be stored.")]
    private static partial void LogNotStored(
        ILogger logger,
        Guid conversationId,
        Exception exception);
}
