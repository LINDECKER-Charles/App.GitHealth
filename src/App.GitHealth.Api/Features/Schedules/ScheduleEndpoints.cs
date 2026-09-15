using App.GitHealth.Api.Features.Common;

namespace App.GitHealth.Api.Features.Schedules;

internal static class ScheduleEndpoints
{
    public static IEndpointRouteBuilder MapScheduleEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/projects/{projectId:guid}/schedule")
            .WithTags("Schedules");
        group.MapGet("/", GetAsync);
        group.MapPut("/", UpdateAsync);
        return endpoints;
    }

    private static async Task<IResult> GetAsync(
        Guid projectId,
        ScheduleService service,
        CancellationToken cancellationToken)
    {
        var result = await service.GetAsync(projectId, cancellationToken);
        return result.IsSuccess ? Results.Ok(result.Value) : ApiProblems.Result(result.Failure!);
    }

    private static async Task<IResult> UpdateAsync(
        Guid projectId,
        ScheduleUpdateRequest request,
        HttpContext context)
    {
        var service = context.RequestServices.GetRequiredService<ScheduleService>();
        var result = await service.UpdateAsync(projectId, request, context.RequestAborted);
        return result.IsSuccess ? Results.Ok(result.Value) : ApiProblems.Result(result.Failure!);
    }
}
