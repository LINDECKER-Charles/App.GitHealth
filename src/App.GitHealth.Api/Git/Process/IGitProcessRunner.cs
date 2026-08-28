namespace App.GitHealth.Api.Git.Process;

internal interface IGitProcessRunner
{
    Task<GitCommandResult> RunAsync(
        GitCommand command,
        CancellationToken cancellationToken);
}
