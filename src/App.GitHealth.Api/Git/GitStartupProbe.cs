using App.GitHealth.Api.Git.Process;

namespace App.GitHealth.Api.Git;

internal sealed class GitStartupProbe(
    IGitProcessRunner runner,
    GitRuntimeDiagnostic diagnostic,
    ILogger<GitStartupProbe> logger) : IHostedService
{
    private static readonly Action<ILogger, string, Exception?> LogGitUnavailable =
        LoggerMessage.Define<string>(
            LogLevel.Warning,
            new EventId(1, nameof(GitStartupProbe)),
            "Git is unavailable at startup. The /health diagnostic exposes the cause: {Reason}");

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

            ReportUnavailable("Git is installed but its diagnostic failed.");
        }
        catch (GitProcessException exception)
        {
            ReportUnavailable(exception.Message);
        }
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _ = cancellationToken;
        return Task.CompletedTask;
    }

    private void ReportUnavailable(string reason)
    {
        diagnostic.ReportUnavailable(reason);
        LogGitUnavailable(logger, reason, null);
    }
}
