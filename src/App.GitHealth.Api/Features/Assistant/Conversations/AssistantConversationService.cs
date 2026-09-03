using App.GitHealth.Api.Features.Common;
using App.GitHealth.Api.Persistence.Models.Assistant;
using App.GitHealth.Api.Persistence.Repositories;
using App.GitHealth.Core.Common;

namespace App.GitHealth.Api.Features.Assistant.Conversations;

/// <summary>
/// Reads back what was kept of past conversations, and removes it. Every removal here is a
/// removal from the database: there is no archive and no soft delete, because the point of
/// the screen offering it is that the user can make the record go away.
/// </summary>
internal sealed class AssistantConversationService(
    IAssistantConversationRepository conversations,
    IProjectRepository projects,
    IClock clock)
{
    public async Task<ApiOutcome<AssistantConversationListResponse>> ListAsync(
        Guid projectId,
        CancellationToken cancellationToken)
    {
        if (!await ExistsAsync(projectId, cancellationToken))
        {
            return ApiOutcome<AssistantConversationListResponse>.Failed(ProjectNotFound());
        }

        var rows = await conversations.ListAsync(projectId, cancellationToken);
        return ApiOutcome<AssistantConversationListResponse>.Success(
            new AssistantConversationListResponse
            {
                Conversations = [.. rows.Select(AssistantConversationSummaryResponse.From)],
            });
    }

    public async Task<ApiOutcome<AssistantConversationResponse>> GetAsync(
        Guid conversationId,
        CancellationToken cancellationToken)
    {
        var thread = await conversations.GetAsync(conversationId, cancellationToken);
        return thread is null
            ? ApiOutcome<AssistantConversationResponse>.Failed(ConversationNotFound())
            : ApiOutcome<AssistantConversationResponse>.Success(
                AssistantConversationResponse.From(thread));
    }

    public async Task<ApiOutcome<AssistantStatusResponse>> GetStatusAsync(
        Guid projectId,
        CancellationToken cancellationToken)
    {
        var project = await projects.GetAsync(projectId, cancellationToken);
        if (project is null)
        {
            return ApiOutcome<AssistantStatusResponse>.Failed(ProjectNotFound());
        }

        return ApiOutcome<AssistantStatusResponse>.Success(new AssistantStatusResponse
        {
            ConsentGrantedAtUtc = project.AssistantConsentAtUtc,
            ConversationCount = await conversations.CountAsync(projectId, cancellationToken),
        });
    }

    /// <summary>
    /// Withdrawing leaves the stored conversations alone. They are removed on purpose from
    /// the screen that lists them, so that saying "stop sending" and saying "forget what was
    /// already said" stay two separate decisions.
    /// </summary>
    public async Task<ApiOutcome<AssistantStatusResponse>> SetConsentAsync(
        Guid projectId,
        bool granted,
        CancellationToken cancellationToken)
    {
        if (!await ExistsAsync(projectId, cancellationToken))
        {
            return ApiOutcome<AssistantStatusResponse>.Failed(ProjectNotFound());
        }

        var now = clock.UtcNow;
        await projects.SetAssistantConsentAsync(
            new AssistantConsentUpdate
            {
                ProjectId = projectId,
                GrantedAtUtc = granted ? now : null,
                ChangedAtUtc = now,
            },
            cancellationToken);
        return await GetStatusAsync(projectId, cancellationToken);
    }

    public async Task<ApiOutcome<bool>> DeleteAsync(
        Guid conversationId,
        CancellationToken cancellationToken)
    {
        var deleted = await conversations.DeleteAsync(conversationId, cancellationToken);
        return deleted
            ? ApiOutcome<bool>.Success(true)
            : ApiOutcome<bool>.Failed(ConversationNotFound());
    }

    public async Task<ApiOutcome<AssistantPurgeResponse>> PurgeAsync(
        Guid projectId,
        CancellationToken cancellationToken)
    {
        if (!await ExistsAsync(projectId, cancellationToken))
        {
            return ApiOutcome<AssistantPurgeResponse>.Failed(ProjectNotFound());
        }

        var deleted = await conversations.PurgeAsync(projectId, cancellationToken);
        return ApiOutcome<AssistantPurgeResponse>.Success(
            new AssistantPurgeResponse { Deleted = deleted });
    }

    private async Task<bool> ExistsAsync(Guid projectId, CancellationToken cancellationToken) =>
        await projects.GetAsync(projectId, cancellationToken) is not null;

    private static ApiFailure ProjectNotFound() => ApiProblems.NotFound(
        ApiErrorCodes.ProjectNotFound,
        "The requested project does not exist.");

    private static ApiFailure ConversationNotFound() => ApiProblems.NotFound(
        ApiErrorCodes.AssistantConversationNotFound,
        "The requested conversation does not exist, or the capture it read was deleted.");
}
