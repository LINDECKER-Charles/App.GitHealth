using App.GitHealth.Api.Features.Common;

namespace App.GitHealth.Api.Features.Snapshots;

internal static class SnapshotCsvEndpoints
{
    public static IEndpointRouteBuilder MapSnapshotCsvEndpoints(
        this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet(
            "/api/projects/{projectId:guid}/analyses/latest/branches.csv",
            ExportAsync).WithTags("Snapshots");
        return endpoints;
    }

    private static async Task<IResult> ExportAsync(
        Guid projectId,
        [AsParameters] SnapshotFilterParameters query,
        HttpContext context)
    {
        var service = context.RequestServices.GetRequiredService<SnapshotService>();
        var result = await service.GetSelectionAsync(
            projectId,
            query,
            context.RequestAborted);
        if (!result.IsSuccess)
        {
            return ApiProblems.Result(result.Failure!);
        }

        var snapshots = result.Value!.Branches.Select(SnapshotMapper.Map);
        var content = SnapshotCsvWriter.Write(snapshots);
        var fileName = $"githealth-branches-{result.Value.Analysis.Id:N}.csv";
        return Results.File(content, "text/csv; charset=utf-8", fileName);
    }
}
