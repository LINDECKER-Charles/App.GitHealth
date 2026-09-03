using App.GitHealth.Api.Features.Common;

namespace App.GitHealth.Api.Features.Assistant.Conversations;

internal static class AssistantConversationEndpoints
{
    private const string Tag = "Assistant";
    private const string ProjectRoute = "/api/projects/{projectId:guid}/assistant";
    private const string ThreadRoute = "/api/assistant/conversations/{conversationId:guid}";

    public static IEndpointRouteBuilder MapAssistantConversationEndpoints(
        this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet($"{ProjectRoute}/status", GetStatusAsync).WithTags(Tag);
        endpoints.MapPut($"{ProjectRoute}/consent", SetConsentAsync).WithTags(Tag);
        endpoints.MapGet($"{ProjectRoute}/conversations", ListAsync).WithTags(Tag);
        endpoints.MapDelete($"{ProjectRoute}/conversations", PurgeAsync).WithTags(Tag);
        endpoints.MapGet(ThreadRoute, GetAsync).WithTags(Tag);
        endpoints.MapDelete(ThreadRoute, DeleteAsync).WithTags(Tag);
        return endpoints;
    }

    private static async Task<IResult> GetStatusAsync(Guid projectId, HttpContext context)
    {
        var service = Service(context);
        var result = await service.GetStatusAsync(projectId, context.RequestAborted);
        return Respond(result);
    }

    private static async Task<IResult> SetConsentAsync(
        Guid projectId,
        AssistantConsentRequest request,
        HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(request);
        var service = Service(context);
        var result = await service.SetConsentAsync(
            projectId,
            request.Granted,
            context.RequestAborted);
        return Respond(result);
    }

    private static async Task<IResult> ListAsync(Guid projectId, HttpContext context)
    {
        var service = Service(context);
        var result = await service.ListAsync(projectId, context.RequestAborted);
        return Respond(result);
    }

    private static async Task<IResult> GetAsync(Guid conversationId, HttpContext context)
    {
        var service = Service(context);
        var result = await service.GetAsync(conversationId, context.RequestAborted);
        return Respond(result);
    }

    private static async Task<IResult> DeleteAsync(Guid conversationId, HttpContext context)
    {
        var service = Service(context);
        var result = await service.DeleteAsync(conversationId, context.RequestAborted);
        return result.IsSuccess ? Results.NoContent() : ApiProblems.Result(result.Failure!);
    }

    /// <summary>
    /// Empties the history of one repository. It reports how many threads went, because
    /// "purged" and "there was nothing to purge" are worth telling apart.
    /// </summary>
    private static async Task<IResult> PurgeAsync(Guid projectId, HttpContext context)
    {
        var service = Service(context);
        var result = await service.PurgeAsync(projectId, context.RequestAborted);
        return Respond(result);
    }

    private static AssistantConversationService Service(HttpContext context) =>
        context.RequestServices.GetRequiredService<AssistantConversationService>();

    private static IResult Respond<T>(ApiOutcome<T> result) => result.IsSuccess
        ? Results.Ok(result.Value)
        : ApiProblems.Result(result.Failure!);
}
