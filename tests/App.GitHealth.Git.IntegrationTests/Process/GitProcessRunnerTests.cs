using App.GitHealth.Api.Git;
using App.GitHealth.Api.Git.Process;
using App.GitHealth.Core.Analysis;
using App.GitHealth.Git.IntegrationTests.Fixtures;
using Microsoft.Extensions.Options;

namespace App.GitHealth.Git.IntegrationTests.Process;

public sealed class GitProcessRunnerTests
{
    [Fact]
    public async Task OutputLimitStopsTheCommandWithAControlledError()
    {
        var options = Options.Create(new GitScannerOptions
        {
            MaximumOutputCharacters = 4,
        });
        var runner = new GitProcessRunner(options);
        var command = GitCommand.Create(Environment.CurrentDirectory, ["--version"]);

        var exception = await Assert.ThrowsAsync<GitProcessException>(() =>
            runner.RunAsync(command, default));

        Assert.Equal(RepositoryErrorCode.MalformedOutput, exception.Code);
    }

    [Fact]
    public async Task PreCancelledCommandPropagatesCancellation()
    {
        var options = Options.Create(new GitScannerOptions());
        var runner = new GitProcessRunner(options);
        var command = GitCommand.Create(Environment.CurrentDirectory, ["--version"]);
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            runner.RunAsync(command, cancellation.Token));
    }

    [Fact]
    public async Task TimeoutStopsALongRunningGitProcess()
    {
        using var repository = GitTestRepository.Create();
        var options = Options.Create(new GitScannerOptions
        {
            CommandTimeout = TimeSpan.FromMilliseconds(100),
        });
        var runner = new GitProcessRunner(options);
        var command = GitCommand.Create(
            Environment.CurrentDirectory,
            ["daemon", "--verbose", "--listen=127.0.0.1", "--port=0",
                $"--base-path={repository.RepositoryPath}", repository.RepositoryPath]);

        var exception = await Assert.ThrowsAsync<GitProcessException>(() =>
            runner.RunAsync(command, default));

        Assert.Equal(RepositoryErrorCode.TimedOut, exception.Code);
    }

    [Fact]
    public async Task CancellationStopsTheEntireDescendantProcessTree()
    {
        using var probe = new ProcessTreeProbe();
        var options = Options.Create(new GitScannerOptions
        {
            CommandTimeout = TimeSpan.FromSeconds(15),
        });
        var runner = new GitProcessRunner(options);
        using var cancellation = new CancellationTokenSource();
        var execution = runner.RunAsync(probe.CreateCommand(), cancellation.Token);
        var processIds = await probe.WaitForProcessesAsync(execution);

        await cancellation.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => execution);
        await ProcessTreeProbe.AssertStoppedAsync(processIds.Parent, processIds.Child);
    }
}
