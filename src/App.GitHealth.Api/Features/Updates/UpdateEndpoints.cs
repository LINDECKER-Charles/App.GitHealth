namespace App.GitHealth.Api.Features.Updates;

internal static class UpdateEndpoints
{
    public static IEndpointRouteBuilder MapUpdateEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/updates", GetStatus).WithTags("Updates");
        endpoints.MapPost("/api/updates/apply", ApplyAsync).WithTags("Updates");
        return endpoints;
    }

    private static async Task<IResult> GetStatus(
        IUpdateService updates,
        CancellationToken cancellationToken) =>
        Results.Ok(await updates.GetStatusAsync(cancellationToken));

    /// <summary>
    /// Downloads the update, then schedules it to be applied after the response is
    /// emitted: applying restarts the process, and the response would never be sent.
    /// </summary>
    private static async Task<IResult> ApplyAsync(
        HttpContext context,
        IUpdateService updates)
    {
        var isReady = await updates.DownloadAsync(context.RequestAborted);
        if (!isReady)
        {
            return Results.Ok(await updates.GetStatusAsync(context.RequestAborted));
        }

        context.Response.OnCompleted(() =>
        {
            updates.ApplyAndRestart();
            return Task.CompletedTask;
        });
        return Results.Accepted();
    }
}
