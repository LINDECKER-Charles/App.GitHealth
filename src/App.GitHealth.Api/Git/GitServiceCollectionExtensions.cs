using App.GitHealth.Api.Git.Process;
using App.GitHealth.Core.Analysis;
using App.GitHealth.Core.Common;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace App.GitHealth.Api.Git;

public static class GitServiceCollectionExtensions
{
    public static IServiceCollection AddGitScanner(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddOptions<GitScannerOptions>()
            .Bind(configuration.GetSection("GitHealth:Git"))
            .PostConfigure(options =>
                options.RepositoriesRoot = configuration["GitHealth:RepositoriesRoot"])
            .ValidateOnStart();
        services.TryAddEnumerable(
            ServiceDescriptor.Singleton<
                IValidateOptions<GitScannerOptions>,
                GitScannerOptionsValidator>());
        services.AddSingleton<IClock, SystemClock>();
        services.AddSingleton(provider => GitExecutableResolver.Capture(
            provider.GetRequiredService<IOptions<GitScannerOptions>>().Value.ExecutablePath));
        services.AddSingleton<IGitProcessRunner, GitProcessRunner>();
        services.AddSingleton<IRepositoryScanner, GitRepositoryScanner>();
        services.AddSingleton<GitRuntimeDiagnostic>();
        services.AddHostedService<GitStartupProbe>();
        services.AddHealthChecks().AddCheck<GitHealthCheck>("git");
        return services;
    }
}
