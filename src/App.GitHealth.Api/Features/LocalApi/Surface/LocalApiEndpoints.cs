using App.GitHealth.Api.Features.Analyses;
using App.GitHealth.Api.Features.Analyses.Lifecycle;
using App.GitHealth.Api.Features.Common;
using App.GitHealth.Api.Features.Projects;
using App.GitHealth.Api.Features.Snapshots;

namespace App.GitHealth.Api.Features.LocalApi.Surface;

/// <summary>
/// What an orchestrator may do with GitHealth: read what has been measured, and ask for a new
/// measurement. Nothing writes to a repository here — the surface is the application's own
/// read model plus the one action it already offers its user, launching an analysis. Every
/// handler resolves its services off <see cref="HttpContext.RequestServices" /> on purpose:
/// those services belong to the application, not to the listener's own container.
/// </summary>
internal static class LocalApiEndpoints
{
    public const string RoutePrefix = "/v1";

    public static IEndpointRouteBuilder MapLocalApiEndpoints(
        this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup(RoutePrefix);
        group.MapGet("/", LocalApiDescription.Describe);
        group.MapGet("/projects", ListProjectsAsync);
        group.MapGet("/projects/{projectId:guid}", GetProjectAsync);
        group.MapGet("/projects/{projectId:guid}/branches", GetLatestBranchesAsync);
        group.MapGet("/projects/{projectId:guid}/analyses", GetHistoryAsync);
        group.MapPost("/projects/{projectId:guid}/analyses", LaunchAsync);
        group.MapGet("/analyses/{analysisId:guid}", GetAnalysisAsync);
        group.MapGet("/analyses/{analysisId:guid}/branches", GetAnalysisBranchesAsync);
        return endpoints;
    }

    private static async Task<IResult> ListProjectsAsync(
        HttpContext context,
        CancellationToken cancellationToken)
    {
        var service = context.RequestServices.GetRequiredService<ProjectService>();
        return Results.Ok(await service.ListAsync(cancellationToken));
    }

    private static async Task<IResult> GetProjectAsync(Guid projectId, HttpContext context)
    {
        var service = context.RequestServices.GetRequiredService<ProjectService>();
        var result = await service.GetAsync(projectId, context.RequestAborted);
        return result.IsSuccess ? Results.Ok(result.Value) : ApiProblems.Result(result.Failure!);
    }

    private static async Task<IResult> GetLatestBranchesAsync(
        Guid projectId,
        [AsParameters] SnapshotQueryParameters query,
        HttpContext context)
    {
        var service = context.RequestServices.GetRequiredService<SnapshotService>();
        var result = await service.GetPageAsync(projectId, query, context.RequestAborted);
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

    /// <summary>
    /// Without a baseline, every baseline the project declares is measured — the same rule the
    /// interface's own button follows, so a scheduled run and a clicked one produce the same
    /// captures.
    /// </summary>
    private static async Task<IResult> LaunchAsync(
        Guid projectId,
        [AsParameters] AnalysisLaunchQueryParameters query,
        HttpContext context)
    {
        var service = context.RequestServices.GetRequiredService<AnalysisLaunchService>();
        var result = await service.LaunchAsync(projectId, query, context.RequestAborted);
        if (!result.IsSuccess)
        {
            return ApiProblems.Result(result.Failure!);
        }

        var launch = LocalApiDescription.Map(result.Value!);
        return Results.Accepted(launch.StatusUrl, launch);
    }

    private static async Task<IResult> GetAnalysisAsync(Guid analysisId, HttpContext context)
    {
        var service = context.RequestServices.GetRequiredService<AnalysisStatusService>();
        var result = await service.GetAsync(analysisId, context.RequestAborted);
        return result.IsSuccess ? Results.Ok(result.Value) : ApiProblems.Result(result.Failure!);
    }

    private static async Task<IResult> GetAnalysisBranchesAsync(
        Guid analysisId,
        [AsParameters] SnapshotQueryParameters query,
        HttpContext context)
    {
        var service = context.RequestServices.GetRequiredService<SnapshotService>();
        var result = await service.GetAnalysisPageAsync(analysisId, query, context.RequestAborted);
        return result.IsSuccess ? Results.Ok(result.Value) : ApiProblems.Result(result.Failure!);
    }
}
