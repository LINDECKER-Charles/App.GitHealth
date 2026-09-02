using App.GitHealth.Api.Features.Common;
using App.GitHealth.Core.Assistant;

namespace App.GitHealth.Api.Features.Assistant;

internal static class AssistantEndpoints
{
    private const string Tag = "Assistant";

    public static IEndpointRouteBuilder MapAssistantEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/assistant/agents", GetAgentsAsync).WithTags(Tag);
        endpoints.MapGet("/api/projects/{projectId:guid}/assistant/briefing", GetBriefingAsync)
            .WithTags(Tag);
        endpoints.MapPost("/api/projects/{projectId:guid}/assistant/runs", StartAsync)
            .WithTags(Tag);
        endpoints.MapGet("/api/assistant/runs/{runId:guid}", GetRun).WithTags(Tag);
        endpoints.MapPost("/api/assistant/runs/{runId:guid}/cancel", CancelRun).WithTags(Tag);
        return endpoints;
    }

    private static async Task<IResult> GetAgentsAsync(
        [AsParameters] AssistantAgentsQueryParameters query,
        HttpContext context)
    {
        var availability = context.RequestServices.GetRequiredService<AgentAvailabilityService>();
        var agents = availability.IsEnabled
            ? await availability.ReadAsync(query.Refresh ?? false, context.RequestAborted)
            : [];
        return Results.Ok(new AssistantAgentListResponse
        {
            IsEnabled = availability.IsEnabled,
            Agents = [.. agents.Select(AssistantAgentResponse.From)],
        });
    }

    /// <summary>
    /// The text an agent would be handed, exactly as it would be handed over. It is read
    /// before a run, not after: nothing leaves the machine that was not shown first.
    /// </summary>
    private static async Task<IResult> GetBriefingAsync(
        Guid projectId,
        [AsParameters] AssistantBriefingQueryParameters query,
        HttpContext context)
    {
        var service = context.RequestServices.GetRequiredService<AssistantBriefingService>();
        var result = await service.BuildAsync(projectId, query.Baseline, context.RequestAborted);
        return result.IsSuccess
            ? Results.Ok(Describe(result.Value!))
            : ApiProblems.Result(result.Failure!);
    }

    private static async Task<IResult> StartAsync(
        Guid projectId,
        AssistantRunRequest request,
        HttpContext context)
    {
        var service = context.RequestServices.GetRequiredService<AssistantRunService>();
        var result = await service.StartAsync(projectId, request, context.RequestAborted);
        return result.IsSuccess
            ? Results.Accepted($"/api/assistant/runs/{result.Value!.RunId}", result.Value)
            : ApiProblems.Result(result.Failure!);
    }

    private static IResult GetRun(
        Guid runId,
        [AsParameters] AssistantRunQueryParameters query,
        HttpContext context)
    {
        var service = context.RequestServices.GetRequiredService<AssistantRunService>();
        var result = service.Read(runId, Math.Max(0, query.From ?? 0));
        return result.IsSuccess ? Results.Ok(result.Value) : ApiProblems.Result(result.Failure!);
    }

    private static IResult CancelRun(Guid runId, HttpContext context)
    {
        var service = context.RequestServices.GetRequiredService<AssistantRunService>();
        var result = service.Cancel(runId);
        return result.IsSuccess ? Results.Ok(result.Value) : ApiProblems.Result(result.Failure!);
    }

    private static AssistantBriefingResponse Describe(AnalysisBriefing briefing) => new()
    {
        Baseline = briefing.Baseline,
        CapturedAtUtc = briefing.CapturedAt,
        BranchCount = briefing.Branches.Count,
        OmittedBranchCount = briefing.OmittedBranchCount,
        Text = BriefingWriter.Write(briefing),
    };
}
