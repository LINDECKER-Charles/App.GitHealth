using App.GitHealth.Api.Features.Common;
using App.GitHealth.Api.Features.Projects;
using Microsoft.Extensions.Options;

namespace App.GitHealth.Api.Features.Runtime;

internal static class RuntimeEndpoints
{
    private const string DockerMode = "docker";
    private const string NativeMode = "native";

    public static IEndpointRouteBuilder MapRuntimeEndpoints(
        this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/runtime", GetRuntimeInfo).WithTags("Runtime");
        endpoints.MapGet("/api/runtime/directories", GetDirectories).WithTags("Runtime");
        return endpoints;
    }

    private static IResult GetRuntimeInfo(IOptions<RepositoryAccessOptions> options)
    {
        var repositoriesRoot = NormalizeRoot(options.Value.RepositoriesRoot);
        var canBrowseDirectories = repositoriesRoot is null;
        return Results.Ok(new RuntimeInfoResponse
        {
            InitialRepositoryPath = NormalizeRoot(options.Value.InitialRepositoryPath),
            RepositoriesRoot = repositoriesRoot,
            CanBrowseDirectories = canBrowseDirectories,
            Mode = canBrowseDirectories ? NativeMode : DockerMode,
        });
    }

    private static IResult GetDirectories(
        string? path,
        IOptions<RepositoryAccessOptions> options)
    {
        if (NormalizeRoot(options.Value.RepositoriesRoot) is not null)
        {
            return ApiProblems.Result(ApiProblems.NotFound(
                ApiErrorCodes.DirectoryBrowsingUnavailable,
                "L’explorateur de dossiers est indisponible dans ce mode."));
        }

        var result = DirectoryBrowser.Browse(path);
        return result.IsSuccess ? Results.Ok(result.Value) : ApiProblems.Result(result.Failure!);
    }

    private static string? NormalizeRoot(string? configuredRoot) =>
        string.IsNullOrWhiteSpace(configuredRoot) ? null : configuredRoot;
}
