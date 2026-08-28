using App.GitHealth.Api.Git.Process;

namespace App.GitHealth.Api.Git;

internal sealed class GitStartupProbe(
    IGitProcessRunner runner,
    GitRuntimeDiagnostic diagnostic) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var command = GitCommand.Create(Environment.CurrentDirectory, ["--version"]);
        try
        {
            var result = await runner.RunAsync(command, cancellationToken);
            if (result.ExitCode == 0)
            {
                diagnostic.ReportAvailable(result.StandardOutput.Trim());
                return;
            }

            diagnostic.ReportUnavailable("Git est installé mais son diagnostic a échoué.");
        }
        catch (GitProcessException exception)
        {
            diagnostic.ReportUnavailable(exception.Message);
        }
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _ = cancellationToken;
        return Task.CompletedTask;
    }
}
