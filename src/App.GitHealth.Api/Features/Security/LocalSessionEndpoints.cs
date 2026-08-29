namespace App.GitHealth.Api.Features.Security;

internal static class LocalSessionEndpoints
{
    public const string Path = "/api/session";

    public static IEndpointRouteBuilder MapLocalSessionEndpoint(
        this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet(Path, () => Results.NoContent())
            .ExcludeFromDescription();
        return endpoints;
    }
}
