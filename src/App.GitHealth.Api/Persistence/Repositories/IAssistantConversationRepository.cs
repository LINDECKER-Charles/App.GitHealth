using App.GitHealth.Api.Persistence.Entities;
using App.GitHealth.Api.Persistence.Models.Assistant;

namespace App.GitHealth.Api.Persistence.Repositories;

internal interface IAssistantConversationRepository
{
    /// <summary>
    /// Writes a settled exchange, opening the thread if this is its first. False when the
    /// capture it read has been deleted in the meantime: the thread has nothing to hang off.
    /// </summary>
    Task<bool> AppendAsync(AssistantTurnRecord turn, CancellationToken cancellationToken);

    Task<IReadOnlyList<AssistantConversationSummary>> ListAsync(
        Guid projectId,
        CancellationToken cancellationToken);

    Task<AssistantConversationEntity?> GetAsync(
        Guid conversationId,
        CancellationToken cancellationToken);

    Task<int> CountAsync(Guid projectId, CancellationToken cancellationToken);

    Task<bool> DeleteAsync(Guid conversationId, CancellationToken cancellationToken);

    /// <summary>Removes every conversation of a project and reports how many there were.</summary>
    Task<int> PurgeAsync(Guid projectId, CancellationToken cancellationToken);
}
