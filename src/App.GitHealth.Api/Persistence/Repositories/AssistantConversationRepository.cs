using App.GitHealth.Api.Persistence.Entities;
using App.GitHealth.Api.Persistence.Models.Assistant;
using Microsoft.EntityFrameworkCore;

namespace App.GitHealth.Api.Persistence.Repositories;

/// <summary>
/// Reads and writes stored conversations. Unlike its siblings this one is a singleton: a
/// turn is written when the agent stops, which happens on a background task long after the
/// request that started it has ended. It holds no context of its own, only the factory, so
/// there is nothing scoped for the later thread to be using out of its lifetime.
/// </summary>
internal sealed class AssistantConversationRepository(
    IDbContextFactory<GitHealthDbContext> contextFactory) : IAssistantConversationRepository
{
    public Task<bool> AppendAsync(AssistantTurnRecord turn, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(turn);
        return SqliteWriteExecutor.ExecuteAsync(async () =>
        {
            await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
            var capture = await context.AnalysisRuns
                .AnyAsync(run => run.Id == turn.AnalysisRunId, cancellationToken);
            if (!capture)
            {
                return false;
            }

            await AppendToThreadAsync(context, turn, cancellationToken);
            await context.SaveChangesAsync(cancellationToken);
            return true;
        });
    }

    public async Task<IReadOnlyList<AssistantConversationSummary>> ListAsync(
        Guid projectId,
        CancellationToken cancellationToken)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        return await context.AssistantConversations.AsNoTracking()
            .Where(conversation => conversation.AnalysisRun.ProjectId == projectId)
            .OrderByDescending(conversation => conversation.UpdatedAtUtc)
            .ThenBy(conversation => conversation.Id)
            .Select(conversation => new AssistantConversationSummary
            {
                Id = conversation.Id,
                AnalysisRunId = conversation.AnalysisRunId,
                Baseline = conversation.AnalysisRun.ReferenceName,
                AgentId = conversation.AgentId,
                AgentName = conversation.AgentName,
                Title = conversation.Title,
                AnswerCount = conversation.Messages
                    .Count(message => message.Role == AssistantMessageEntity.AgentRole),
                StartedAtUtc = conversation.StartedAtUtc,
                UpdatedAtUtc = conversation.UpdatedAtUtc,
            })
            .ToListAsync(cancellationToken);
    }

    public async Task<AssistantConversationEntity?> GetAsync(
        Guid conversationId,
        CancellationToken cancellationToken)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        return await context.AssistantConversations.AsNoTracking()
            .Include(conversation => conversation.AnalysisRun)
            .Include(conversation => conversation.Messages)
            .SingleOrDefaultAsync(
                conversation => conversation.Id == conversationId,
                cancellationToken);
    }

    public async Task<int> CountAsync(Guid projectId, CancellationToken cancellationToken)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        return await context.AssistantConversations.AsNoTracking()
            .CountAsync(
                conversation => conversation.AnalysisRun.ProjectId == projectId,
                cancellationToken);
    }

    public Task<bool> DeleteAsync(Guid conversationId, CancellationToken cancellationToken)
    {
        return SqliteWriteExecutor.ExecuteAsync(async () =>
        {
            await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
            var deleted = await context.AssistantConversations
                .Where(conversation => conversation.Id == conversationId)
                .ExecuteDeleteAsync(cancellationToken);
            return deleted > 0;
        });
    }

    /// <summary>The messages go with the threads: the database cascade removes them.</summary>
    public Task<int> PurgeAsync(Guid projectId, CancellationToken cancellationToken)
    {
        return SqliteWriteExecutor.ExecuteAsync(async () =>
        {
            await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
            return await context.AssistantConversations
                .Where(conversation => conversation.AnalysisRun.ProjectId == projectId)
                .ExecuteDeleteAsync(cancellationToken);
        });
    }

    private static async Task AppendToThreadAsync(
        GitHealthDbContext context,
        AssistantTurnRecord turn,
        CancellationToken cancellationToken)
    {
        var conversation = await context.AssistantConversations
            .Include(candidate => candidate.Messages)
            .SingleOrDefaultAsync(
                candidate => candidate.Id == turn.ConversationId,
                cancellationToken);
        if (conversation is null)
        {
            context.AssistantConversations.Add(AssistantConversationEntity.Open(turn));
            return;
        }

        conversation.Append(turn);
    }
}
