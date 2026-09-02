using App.GitHealth.Api.Features.Analyses;
using App.GitHealth.Api.Features.Analyses.Lifecycle;
using App.GitHealth.Api.Features.Assistant;
using App.GitHealth.Api.Features.Baselines;
using App.GitHealth.Api.Features.Common;
using App.GitHealth.Api.Features.Discovery;
using App.GitHealth.Api.Features.Policies;
using App.GitHealth.Api.Features.Projects;
using App.GitHealth.Api.Features.Snapshots;

namespace App.GitHealth.Api.Features;

internal static class GitHealthApiServiceCollectionExtensions
{
    public static IServiceCollection AddGitHealthApi(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddProblemDetails(ConfigureProblemDetails);
        services.AddExceptionHandler<PersistenceExceptionHandler>();
        services.AddOptions<RepositoryAccessOptions>()
            .Bind(configuration.GetSection(RepositoryAccessOptions.SectionName));
        services.AddOptions<AnalysisQueueOptions>()
            .Bind(configuration.GetSection(AnalysisQueueOptions.SectionName))
            .Validate(IsQueueCapacityValid, "Invalid analysis queue capacity.")
            .Validate(IsAnalysisTimeoutValid, "Invalid overall analysis timeout.")
            .Validate(IsParallelAnalysisCountValid, "Invalid analysis parallelism.")
            .ValidateOnStart();
        services.AddAssistant(configuration);
        services.AddSingleton<RepositoryValidator>();
        services.AddScoped<RepositoryDiscoveryService>();
        services.AddScoped<ProjectService>();
        services.AddScoped<ProjectOrganizationService>();
        services.AddScoped<ProjectDeletionService>();
        services.AddScoped<PolicyService>();
        services.AddScoped<BaselineService>();
        services.AddSingleton<AnalysisQueue>();
        services.AddHostedService<AnalysisWorker>();
        services.AddScoped<AnalysisStatusService>();
        services.AddScoped<AnalysisHistoryService>();
        services.AddScoped<AnalysisLaunchService>();
        services.AddScoped<AnalysisDeletionService>();
        services.AddScoped<SnapshotMapper>();
        services.AddScoped<SnapshotService>();
        return services;
    }

    private static bool IsQueueCapacityValid(AnalysisQueueOptions options) =>
        options.Capacity is > 0 and <= AnalysisQueueOptions.MaximumCapacity;

    private static bool IsAnalysisTimeoutValid(AnalysisQueueOptions options) =>
        options.TimeoutSeconds is >= AnalysisQueueOptions.MinimumTimeoutSeconds
            and <= AnalysisQueueOptions.MaximumTimeoutSeconds;

    private static bool IsParallelAnalysisCountValid(AnalysisQueueOptions options) =>
        options.MaximumParallelAnalyses is >= AnalysisQueueOptions.MinimumParallelAnalyses
            and <= AnalysisQueueOptions.MaximumParallelAnalysesLimit;

    private static void ConfigureProblemDetails(ProblemDetailsOptions options)
    {
        options.CustomizeProblemDetails = context =>
        {
            context.ProblemDetails.Extensions.TryAdd(
                "code",
                ApiErrorCodes.InvalidRequest);
            context.ProblemDetails.Extensions.TryAdd(
                "traceId",
                context.HttpContext.TraceIdentifier);
        };
    }
}
