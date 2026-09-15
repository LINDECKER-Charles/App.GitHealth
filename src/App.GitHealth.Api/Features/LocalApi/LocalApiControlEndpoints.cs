using App.GitHealth.Api.Features.Common;

namespace App.GitHealth.Api.Features.LocalApi;

/// <summary>
/// How the interface drives the access. These routes sit on the browser surface, behind the
/// session and the anti-forgery token like every other mutation — opening a port is a decision
/// only the person in front of the window may take, never the orchestrator on the other side.
/// </summary>
internal static class LocalApiControlEndpoints
{
    public const string RoutePrefix = "/api/local-api";

    public static IEndpointRouteBuilder MapLocalApiControlEndpoints(
        this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup(RoutePrefix).WithTags("LocalApi");
        group.MapGet("/", Describe);
        group.MapPut("/", ApplyAsync);
        group.MapPost("/token", IssueToken);
        return endpoints;
    }

    private static IResult Describe(HttpContext context)
    {
        var access = context.RequestServices.GetRequiredService<LocalApiService>();
        return Results.Ok(access.Describe());
    }

    private static async Task<IResult> ApplyAsync(
        LocalApiUpdateRequest request,
        HttpContext context)
    {
        var access = context.RequestServices.GetRequiredService<LocalApiService>();
        var result = await access.ApplyAsync(request, context.RequestAborted);
        return result.IsSuccess ? Results.Ok(result.Value) : ApiProblems.Result(result.Failure!);
    }

    /// <summary>The token is in this answer and in no other. Losing it means issuing another.</summary>
    private static IResult IssueToken(HttpContext context)
    {
        var access = context.RequestServices.GetRequiredService<LocalApiService>();
        return Results.Ok(access.IssueToken());
    }
}
