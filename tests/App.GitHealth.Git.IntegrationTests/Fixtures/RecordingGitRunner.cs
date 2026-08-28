using App.GitHealth.Api.Git.Process;

namespace App.GitHealth.Git.IntegrationTests.Fixtures;

internal sealed class RecordingGitRunner : IGitProcessRunner
{
    private readonly Queue<GitCommandResult> _results = new();

    public List<GitCommand> Commands { get; } = [];

    public void Enqueue(GitCommandResult result) => _results.Enqueue(result);

    public Task<GitCommandResult> RunAsync(
        GitCommand command,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Commands.Add(command);
        return Task.FromResult(_results.Dequeue());
    }
}
