using App.GitHealth.Api.Features.Common;

namespace App.GitHealth.Api.Features.Snapshots;

internal static class SnapshotEndpoints
{
    public static IEndpointRouteBuilder MapSnapshotEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet(
            "/api/projects/{projectId:guid}/analyses/latest/branches",
            GetPageAsync).WithTags("Snapshots");
        endpoints.MapGet("/api/branch-snapshots/{snapshotId:guid}", GetDetailAsync)
            .WithTags("Snapshots");
        return endpoints;
    }

    private static async Task<IResult> GetPageAsync(
        Guid projectId,
        [AsParameters] SnapshotQueryParameters query,
        HttpContext context)
    {
        var service = context.RequestServices.GetRequiredService<SnapshotService>();
        var result = await service.GetPageAsync(projectId, query, context.RequestAborted);
        return result.IsSuccess ? Results.Ok(result.Value) : ApiProblems.Result(result.Failure!);
    }

    private static async Task<IResult> GetDetailAsync(Guid snapshotId, HttpContext context)
    {
        var service = context.RequestServices.GetRequiredService<SnapshotService>();
        var result = await service.GetDetailAsync(snapshotId, context.RequestAborted);
        return result.IsSuccess ? Results.Ok(result.Value) : ApiProblems.Result(result.Failure!);
    }
}
