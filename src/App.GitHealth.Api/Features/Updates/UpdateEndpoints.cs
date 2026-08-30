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
    /// Télécharge la mise à jour puis programme son application après l'émission de la
    /// réponse : appliquer relance le processus, et la réponse ne partirait jamais.
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
