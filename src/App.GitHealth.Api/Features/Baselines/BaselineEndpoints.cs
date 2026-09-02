using App.GitHealth.Api.Features.Common;

namespace App.GitHealth.Api.Features.Baselines;

internal static class BaselineEndpoints
{
    public static IEndpointRouteBuilder MapBaselineEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints
            .MapGroup("/api/projects/{projectId:guid}/baselines")
            .WithTags("Baselines");
        group.MapGet("/", ListAsync);
        group.MapPut("/", ReplaceAsync);
        return endpoints;
    }

    private static async Task<IResult> ListAsync(Guid projectId, HttpContext context)
    {
        var service = context.RequestServices.GetRequiredService<BaselineService>();
        var result = await service.ListAsync(projectId, context.RequestAborted);
        return result.IsSuccess ? Results.Ok(result.Value) : ApiProblems.Result(result.Failure!);
    }

    private static async Task<IResult> ReplaceAsync(
        Guid projectId,
        BaselineUpdateRequest request,
        HttpContext context)
    {
        var service = context.RequestServices.GetRequiredService<BaselineService>();
        var result = await service.ReplaceAsync(projectId, request, context.RequestAborted);
        return result.IsSuccess ? Results.Ok(result.Value) : ApiProblems.Result(result.Failure!);
    }
}
