using System.Diagnostics;
using App.GitHealth.Api.Git.Process;
using App.GitHealth.Core.Analysis;

namespace App.GitHealth.Api.Git.Scanning;

/// <summary>
/// Runs the commands of the underlying runner and says out loud what it just ran. Timing
/// belongs here and nowhere else: it is the only place that sees a command end to end.
/// </summary>
internal sealed class TracedGitProcessRunner(IGitProcessRunner inner, ScanReporter reporter)
    : IGitProcessRunner
{
    /// <summary>Exit code reported when Git never got far enough to return one.</summary>
    private const int NotStartedExitCode = -1;

    public async Task<GitCommandResult> RunAsync(
        GitCommand command,
        CancellationToken cancellationToken)
    {
        var startedAt = Stopwatch.GetTimestamp();
        try
        {
            var result = await inner.RunAsync(command, cancellationToken);
            Report(command, startedAt, result.ExitCode, result.StandardOutput);
            return result;
        }
        catch (GitProcessException exception)
        {
            Report(command, startedAt, NotStartedExitCode, exception.Message);
            throw;
        }
    }

    private void Report(GitCommand command, long startedAt, int exitCode, string output)
    {
        reporter.CommandCompleted(new ScanCommandCompleted
        {
            CommandLine = GitCommandLine.Describe(command),
            Duration = Stopwatch.GetElapsedTime(startedAt),
            ExitCode = exitCode,
            Output = GitCommandLine.SummariseOutput(output),
        });
    }
}
