using System.Reflection;
using App.GitHealth.Api.Features.Analyses;

namespace App.GitHealth.Api.Features.LocalApi.Surface;

/// <summary>
/// What an orchestrator reads first: which surface it reached, which GitHealth serves it, and
/// every route it may call. A caller that has to be told the route list by a human is a caller
/// that breaks silently when the list changes.
/// </summary>
internal static class LocalApiDescription
{
    private const string SurfaceName = "GitHealth local API";
    private const string SurfaceVersion = "v1";

    private static readonly string[] Routes =
    [
        "GET " + LocalApiEndpoints.RoutePrefix + "/projects",
        "GET " + LocalApiEndpoints.RoutePrefix + "/projects/{projectId}",
        "GET " + LocalApiEndpoints.RoutePrefix + "/projects/{projectId}/branches",
        "GET " + LocalApiEndpoints.RoutePrefix + "/projects/{projectId}/analyses",
        "POST " + LocalApiEndpoints.RoutePrefix + "/projects/{projectId}/analyses",
        "GET " + LocalApiEndpoints.RoutePrefix + "/analyses/{analysisId}",
        "GET " + LocalApiEndpoints.RoutePrefix + "/analyses/{analysisId}/branches",
    ];

    public static LocalApiIndexResponse Describe() => new()
    {
        Name = SurfaceName,
        Surface = SurfaceVersion,
        Application = ApplicationVersion(),
        Routes = Routes,
    };

    /// <summary>
    /// The launch answer is rewritten rather than forwarded: the interface's own status URL
    /// points into <c>/api</c>, which this caller cannot reach and must never be sent to.
    /// </summary>
    public static LocalApiLaunchResponse Map(AnalysisLaunchResponse launch) => new()
    {
        Analyses = launch.Analyses
            .Select(item => new LocalApiLaunchItem
            {
                AnalysisId = item.AnalysisId,
                ReferenceName = item.ReferenceName,
                StatusUrl = StatusUrl(item.AnalysisId),
                IsDuplicate = item.IsDuplicate,
            })
            .ToArray(),
        AnalysisId = launch.AnalysisId,
        StatusUrl = StatusUrl(launch.AnalysisId),
        IsDuplicate = launch.IsDuplicate,
    };

    private static string StatusUrl(Guid analysisId) =>
        $"{LocalApiEndpoints.RoutePrefix}/analyses/{analysisId}";

    private static string ApplicationVersion() =>
        typeof(Program).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
            .InformationalVersion
        ?? typeof(Program).Assembly.GetName().Version?.ToString()
        ?? "unknown";
}

internal sealed record LocalApiIndexResponse
{
    public required string Name { get; init; }

    public required string Surface { get; init; }

    public required string Application { get; init; }

    public required IReadOnlyList<string> Routes { get; init; }
}

internal sealed record LocalApiLaunchResponse
{
    public required IReadOnlyList<LocalApiLaunchItem> Analyses { get; init; }

    public required Guid AnalysisId { get; init; }

    public required string StatusUrl { get; init; }

    public required bool IsDuplicate { get; init; }
}

internal sealed record LocalApiLaunchItem
{
    public required Guid AnalysisId { get; init; }

    public required string ReferenceName { get; init; }

    public required string StatusUrl { get; init; }

    public required bool IsDuplicate { get; init; }
}
