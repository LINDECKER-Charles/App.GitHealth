using App.GitHealth.Api.Features.Common;

namespace App.GitHealth.Api.Features.Discovery;

internal static class DiscoveryEndpoints
{
    public static IEndpointRouteBuilder MapDiscoveryEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/api/repositories/discover", DiscoverAsync).WithTags("Discovery");
        return endpoints;
    }

    private static async Task<IResult> DiscoverAsync(
        RepositoryDiscoveryRequest request,
        RepositoryDiscoveryService service,
        CancellationToken cancellationToken)
    {
        var result = await service.DiscoverAsync(request, cancellationToken);
        return result.IsSuccess ? Results.Ok(result.Value) : ApiProblems.Result(result.Failure!);
    }
}
