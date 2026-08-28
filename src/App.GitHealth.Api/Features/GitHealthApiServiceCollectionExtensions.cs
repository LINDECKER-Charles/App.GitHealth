using App.GitHealth.Api.Features.Analyses;
using App.GitHealth.Api.Features.Common;
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
            .Validate(IsQueueCapacityValid, "Capacité de file d’analyses invalide.")
            .ValidateOnStart();
        services.AddSingleton<RepositoryValidator>();
        services.AddScoped<ProjectService>();
        services.AddScoped<PolicyService>();
        services.AddSingleton<AnalysisQueue>();
        services.AddHostedService<AnalysisWorker>();
        services.AddScoped<AnalysisStatusService>();
        services.AddScoped<AnalysisHistoryService>();
        services.AddScoped<SnapshotMapper>();
        services.AddScoped<SnapshotService>();
        return services;
    }

    private static bool IsQueueCapacityValid(AnalysisQueueOptions options) =>
        options.Capacity is > 0 and <= AnalysisQueueOptions.MaximumCapacity;

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
