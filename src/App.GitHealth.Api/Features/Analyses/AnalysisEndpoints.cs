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
        endpoints.MapGet("/api/projects/{projectId:guid}/analyses", GetHistoryAsync)
            .WithTags("Analyses");
        return endpoints;
    }

    private static async Task<IResult> LaunchAsync(
        Guid projectId,
        AnalysisQueue queue,
        CancellationToken cancellationToken)
    {
        var result = await queue.EnqueueAsync(projectId, cancellationToken);
        if (result.Kind is AnalysisEnqueueKind.ProjectNotFound
            or AnalysisEnqueueKind.QueueFull)
        {
            return Failure(result.Kind);
        }

        return Accepted(result);
    }

    private static IResult Failure(AnalysisEnqueueKind kind)
    {
        if (kind == AnalysisEnqueueKind.ProjectNotFound)
        {
            return ApiProblems.Result(ApiProblems.NotFound(
                ApiErrorCodes.ProjectNotFound,
                "Le projet demandé n’existe pas."));
        }

        return ApiProblems.Result(ApiProblems.Unavailable(
            ApiErrorCodes.QueueFull,
            "La file d’analyses est pleine."));
    }

    private static IResult Accepted(AnalysisEnqueueResult result)
    {
        var location = $"/api/analyses/{result.AnalysisId}";
        var response = new AnalysisLaunchResponse
        {
            AnalysisId = result.AnalysisId!.Value,
            StatusUrl = location,
            IsDuplicate = result.IsDuplicate,
        };
        return Results.Accepted(location, response);
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
