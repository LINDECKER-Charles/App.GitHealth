using App.GitHealth.Api.Git.Process;
using App.GitHealth.Core.Analysis;
using App.GitHealth.Core.Common;

namespace App.GitHealth.Api.Git;

public static class GitServiceCollectionExtensions
{
    public static IServiceCollection AddGitScanner(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddOptions<GitScannerOptions>()
            .Bind(configuration.GetSection("GitHealth:Git"))
            .Validate(options => options.CommandTimeout > TimeSpan.Zero, "Délai Git invalide.")
            .Validate(options => options.MaximumOutputCharacters > 0, "Limite Git invalide.")
            .Validate(options => options.MaximumParallelCommands > 0, "Concurrence Git invalide.")
            .ValidateOnStart();
        services.AddSingleton<IClock, SystemClock>();
        services.AddSingleton<IGitProcessRunner, GitProcessRunner>();
        services.AddSingleton<IRepositoryScanner, GitRepositoryScanner>();
        services.AddSingleton<GitRuntimeDiagnostic>();
        services.AddHostedService<GitStartupProbe>();
        services.AddHealthChecks().AddCheck<GitHealthCheck>("git");
        return services;
    }
}
