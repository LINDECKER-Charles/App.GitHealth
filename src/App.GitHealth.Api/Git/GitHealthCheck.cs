using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace App.GitHealth.Api.Git;

internal sealed class GitHealthCheck(GitRuntimeDiagnostic diagnostic) : IHealthCheck
{
    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        _ = context;
        _ = cancellationToken;
        var state = diagnostic.Read();
        return Task.FromResult(state.IsAvailable
            ? HealthCheckResult.Healthy(state.Message)
            : HealthCheckResult.Unhealthy(state.Message));
    }
}
