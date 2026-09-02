using App.GitHealth.Api.Features.Analyses;
using App.GitHealth.Api.Features.Baselines;
using App.GitHealth.Api.Features.Discovery;
using App.GitHealth.Api.Features.Exports;
using App.GitHealth.Api.Features.Policies;
using App.GitHealth.Api.Features.Projects;
using App.GitHealth.Api.Features.Runtime;
using App.GitHealth.Api.Features.Security;
using App.GitHealth.Api.Features.Snapshots;
using App.GitHealth.Api.Features.Updates;

namespace App.GitHealth.Api.Features;

internal static class GitHealthApiEndpointRouteBuilderExtensions
{
    public static IEndpointRouteBuilder MapGitHealthApi(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapLocalSessionEndpoint();
        endpoints.MapProjectEndpoints();
        endpoints.MapPolicyEndpoints();
        endpoints.MapBaselineEndpoints();
        endpoints.MapRuntimeEndpoints();
        endpoints.MapDiscoveryEndpoints();
        endpoints.MapAnalysisEndpoints();
        endpoints.MapSnapshotEndpoints();
        endpoints.MapSnapshotCsvEndpoints();
        endpoints.MapDatabaseExportEndpoints();
        endpoints.MapUpdateEndpoints();
        return endpoints;
    }
}
