using App.GitHealth.Api.Features.Assistant.Agents;
using Microsoft.Extensions.Options;

namespace App.GitHealth.Api.Features.Assistant;

internal static class AssistantServiceCollectionExtensions
{
    public static IServiceCollection AddAssistant(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddOptions<AssistantOptions>()
            .Bind(configuration.GetSection(AssistantOptions.SectionName))
            .Validate(IsTimeoutValid, "Invalid assistant run timeout.")
            .Validate(IsOutputBudgetValid, "Invalid assistant output budget.")
            .Validate(IsBranchCapValid, "Invalid assistant briefing size.")
            .Validate(IsParallelismValid, "Invalid assistant parallelism.")
            .ValidateOnStart();

        // The resolver walks the file system once, so it is captured for the process; the
        // runner and the registry hold the state a request must not own.
        services.AddSingleton(provider => AgentExecutableResolver.Capture(
            provider.GetRequiredService<IOptions<AssistantOptions>>()));
        services.AddSingleton<AgentAvailabilityService>();
        services.AddSingleton<AssistantRunRegistry>();
        services.AddScoped<AssistantBriefingService>();
        services.AddScoped<AssistantRunService>();
        return services;
    }

    private static bool IsTimeoutValid(AssistantOptions options) =>
        options.RunTimeout.TotalSeconds is >= AssistantOptions.MinimumTimeoutSeconds
            and <= AssistantOptions.MaximumTimeoutSeconds;

    private static bool IsOutputBudgetValid(AssistantOptions options) =>
        options.MaximumOutputBytes is >= AssistantOptions.MinimumOutputBytes
            and <= AssistantOptions.MaximumOutputBytesLimit;

    private static bool IsBranchCapValid(AssistantOptions options) =>
        options.MaximumBranches is >= AssistantOptions.MinimumBranches
            and <= AssistantOptions.MaximumBranchesLimit;

    private static bool IsParallelismValid(AssistantOptions options) =>
        options.MaximumParallelRuns is >= 1 and <= AssistantOptions.MaximumParallelRunsLimit;
}
