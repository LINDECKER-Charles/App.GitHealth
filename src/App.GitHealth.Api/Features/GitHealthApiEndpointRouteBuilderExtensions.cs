using App.GitHealth.Api.Features.Analyses;
using App.GitHealth.Api.Features.Exports;
using App.GitHealth.Api.Features.Projects;
using App.GitHealth.Api.Features.Snapshots;

namespace App.GitHealth.Api.Features;

internal static class GitHealthApiEndpointRouteBuilderExtensions
{
    public static IEndpointRouteBuilder MapGitHealthApi(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapProjectEndpoints();
        endpoints.MapAnalysisEndpoints();
        endpoints.MapSnapshotEndpoints();
        endpoints.MapDatabaseExportEndpoints();
        return endpoints;
    }
}
