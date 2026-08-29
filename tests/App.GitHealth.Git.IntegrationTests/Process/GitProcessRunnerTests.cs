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
            MaximumOutputBytes = 4,
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

    [Fact]
    public async Task ConcurrencyLimitAppliesAcrossAllGitCommands()
    {
        using var repository = GitTestRepository.Create();
        using var runner = new GitProcessRunner(Options.Create(new GitScannerOptions
        {
            CommandTimeout = TimeSpan.FromSeconds(15),
            MaximumParallelCommands = 1,
        }));
        using var firstCancellation = new CancellationTokenSource();
        using var secondCancellation = new CancellationTokenSource();
        var first = runner.RunAsync(CreateBlockingCommand(repository), firstCancellation.Token);
        await Task.Delay(250);

        var second = runner.RunAsync(
            GitCommand.Create(Environment.CurrentDirectory, ["--version"]),
            secondCancellation.Token);
        await Task.Delay(250);

        Assert.False(second.IsCompleted);
        await secondCancellation.CancelAsync();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => second);
        await firstCancellation.CancelAsync();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => first);
    }

    [Fact]
    public void StartInfoKeepsHostileValuesSeparatedAndDisablesSideEffects()
    {
        const string hostile = "refs/heads/feature;echo-owned";
        var command = GitCommand.Create(
            Environment.CurrentDirectory,
            ["show-ref", "--verify", hostile]);

        var startInfo = GitProcessRunner.CreateStartInfo(command);

        Assert.False(startInfo.UseShellExecute);
        Assert.Contains(hostile, startInfo.ArgumentList);
        Assert.Contains("protocol.allow=never", startInfo.ArgumentList);
        Assert.Contains("credential.helper=", startInfo.ArgumentList);
        Assert.Contains("core.fsmonitor=false", startInfo.ArgumentList);
        Assert.Equal("1", startInfo.Environment["GIT_NO_LAZY_FETCH"]);
        Assert.Equal("0", startInfo.Environment["GIT_TERMINAL_PROMPT"]);
        Assert.Equal("Never", startInfo.Environment["GCM_INTERACTIVE"]);
    }

    [Fact]
    public void HostTraceAndConfigurationOverridesAreRemoved()
    {
        var environment = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
        {
            ["GIT_CONFIG_GLOBAL"] = "hostile-global-config",
            ["GIT_CONFIG_SYSTEM"] = "hostile-system-config",
            ["GIT_TRACE"] = "trace.log",
            ["GIT_TRACE2_EVENT"] = "trace.json",
            ["UNRELATED"] = "preserved",
        };

        GitProcessRunner.RemoveHostGitOverrides(environment);

        Assert.DoesNotContain(
            environment.Keys,
            name => name.StartsWith("GIT_", StringComparison.OrdinalIgnoreCase));
        Assert.Equal("preserved", environment["UNRELATED"]);
    }

    [Fact]
    public void OutputBudgetIsSharedByBothStreamsInBytes()
    {
        var budget = new GitProcessRunner.OutputBudget(4);

        budget.Consume(2);

        var exception = Assert.Throws<GitProcessException>(() => budget.Consume(3));
        Assert.Equal(RepositoryErrorCode.MalformedOutput, exception.Code);
    }

    private static GitCommand CreateBlockingCommand(GitTestRepository repository) =>
        GitCommand.Create(
            Environment.CurrentDirectory,
            ["daemon", "--verbose", "--listen=127.0.0.1", "--port=0",
                $"--base-path={repository.RepositoryPath}", repository.RepositoryPath]);
}
