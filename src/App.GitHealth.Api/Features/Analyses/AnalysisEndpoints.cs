using App.GitHealth.Api.Features.Analyses.Lifecycle;
using App.GitHealth.Api.Features.Common;

namespace App.GitHealth.Api.Features.Analyses;

internal static class AnalysisEndpoints
{
    public static IEndpointRouteBuilder MapAnalysisEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/api/projects/{projectId:guid}/analyses", LaunchAsync)
            .WithTags("Analyses");
        endpoints.MapGet("/api/analyses/{analysisId:guid}", GetStatusAsync)
            .WithTags("Analyses");
        endpoints.MapDelete("/api/analyses/{analysisId:guid}", DeleteAsync)
            .WithTags("Analyses");
        endpoints.MapGet("/api/projects/{projectId:guid}/analyses", GetHistoryAsync)
            .WithTags("Analyses");
        return endpoints;
    }

    private static async Task<IResult> LaunchAsync(
        Guid projectId,
        [AsParameters] AnalysisLaunchQueryParameters query,
        HttpContext context)
    {
        var service = context.RequestServices.GetRequiredService<AnalysisLaunchService>();
        var result = await service.LaunchAsync(projectId, query, context.RequestAborted);
        return result.IsSuccess
            ? Results.Accepted(result.Value!.StatusUrl, result.Value)
            : ApiProblems.Result(result.Failure!);
    }

    private static async Task<IResult> DeleteAsync(Guid analysisId, HttpContext context)
    {
        var service = context.RequestServices.GetRequiredService<AnalysisDeletionService>();
        var result = await service.DeleteAsync(analysisId, context.RequestAborted);
        return result.IsSuccess
            ? Results.NoContent()
            : ApiProblems.Result(result.Failure!);
    }

    private static async Task<IResult> GetStatusAsync(Guid analysisId, HttpContext context)
    {
        var service = context.RequestServices.GetRequiredService<AnalysisStatusService>();
        var result = await service.GetAsync(analysisId, context.RequestAborted);
        return result.IsSuccess ? Results.Ok(result.Value) : ApiProblems.Result(result.Failure!);
    }

    private static async Task<IResult> GetHistoryAsync(
        Guid projectId,
        [AsParameters] AnalysisHistoryQueryParameters query,
        HttpContext context)
    {
        var service = context.RequestServices.GetRequiredService<AnalysisHistoryService>();
        var result = await service.GetAsync(projectId, query, context.RequestAborted);
        return result.IsSuccess ? Results.Ok(result.Value) : ApiProblems.Result(result.Failure!);
    }
}
