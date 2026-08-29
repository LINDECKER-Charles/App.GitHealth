using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Text;
using App.GitHealth.Core.Analysis;
using Microsoft.Extensions.Options;
using DiagnosticsProcess = System.Diagnostics.Process;

namespace App.GitHealth.Api.Git.Process;

internal sealed class GitProcessRunner : IGitProcessRunner, IDisposable
{
    private const int ReadBufferSize = 4096;
    private readonly SemaphoreSlim _concurrency;
    private readonly GitScannerOptions _options;

    public GitProcessRunner(IOptions<GitScannerOptions> options)
    {
        _options = options.Value;
        _concurrency = new SemaphoreSlim(
            _options.MaximumParallelCommands,
            _options.MaximumParallelCommands);
    }

    public async Task<GitCommandResult> RunAsync(
        GitCommand command,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await _concurrency.WaitAsync(cancellationToken);
        try
        {
            using var process = StartProcess(command);
            process.StandardInput.Close();
            return await MonitorAsync(process, cancellationToken);
        }
        finally
        {
            _concurrency.Release();
        }
    }

    public void Dispose() => _concurrency.Dispose();

    private async Task<GitCommandResult> MonitorAsync(
        DiagnosticsProcess process,
        CancellationToken cancellationToken)
    {
        using var timeout = new CancellationTokenSource(_options.CommandTimeout);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken,
            timeout.Token);

        try
        {
            return await AwaitCompletionAsync(process, linked.Token);
        }
        catch (Exception exception) when (exception is GitProcessException
            or OperationCanceledException)
        {
            await linked.CancelAsync();
            Kill(process);
            if (exception is OperationCanceledException
                && !cancellationToken.IsCancellationRequested)
            {
                throw new GitProcessException(
                    RepositoryErrorCode.TimedOut,
                    "La commande Git a dépassé le délai autorisé.");
            }

            throw;
        }
    }

    private async Task<GitCommandResult> AwaitCompletionAsync(
        DiagnosticsProcess process,
        CancellationToken cancellationToken)
    {
        var budget = new OutputBudget(_options.MaximumOutputBytes);
        var outputTask = ReadBoundedAsync(
            process.StandardOutput.BaseStream,
            budget,
            cancellationToken);
        var errorTask = ReadBoundedAsync(
            process.StandardError.BaseStream,
            budget,
            cancellationToken);
        var exitTask = process.WaitForExitAsync(cancellationToken);
        var first = await Task.WhenAny(exitTask, outputTask, errorTask);
        if (first != exitTask)
        {
            await first;
        }

        await exitTask;
        var streams = await Task.WhenAll(outputTask, errorTask);
        return new GitCommandResult(process.ExitCode, streams[0], streams[1]);
    }

    private static DiagnosticsProcess StartProcess(GitCommand command)
    {
        var process = new DiagnosticsProcess { StartInfo = CreateStartInfo(command) };
        try
        {
            process.Start();
            return process;
        }
        catch (Exception exception) when (exception is System.ComponentModel.Win32Exception
            or InvalidOperationException)
        {
            process.Dispose();
            throw new GitProcessException(
                RepositoryErrorCode.GitUnavailable,
                "Git est introuvable ou ne peut pas être démarré.",
                exception);
        }
    }

    internal static ProcessStartInfo CreateStartInfo(GitCommand command)
    {
        var startInfo = new ProcessStartInfo("git")
        {
            WorkingDirectory = command.WorkingDirectory,
            UseShellExecute = false,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
            CreateNoWindow = true,
        };

        AddConfiguration(startInfo.ArgumentList);
        foreach (var argument in command.Arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        ConfigureEnvironment(startInfo.Environment);
        return startInfo;
    }

    private static async Task<string> ReadBoundedAsync(
        Stream reader,
        OutputBudget budget,
        CancellationToken cancellationToken)
    {
        var buffer = new byte[ReadBufferSize];
        using var content = new MemoryStream();
        while (true)
        {
            var count = await reader.ReadAsync(buffer, cancellationToken);
            if (count == 0)
            {
                return Encoding.UTF8.GetString(content.GetBuffer(), 0, (int)content.Length);
            }

            budget.Consume(count);
            await content.WriteAsync(buffer.AsMemory(0, count), cancellationToken);
        }
    }

    private static void AddConfiguration(Collection<string> arguments)
    {
        arguments.Add("--no-pager");
        AddConfiguration(arguments, "color.ui=false");
        AddConfiguration(arguments, "protocol.allow=never");
        AddConfiguration(arguments, "protocol.file.allow=never");
        AddConfiguration(arguments, "credential.helper=");
        AddConfiguration(arguments, "core.fsmonitor=false");
        AddConfiguration(arguments, "mailmap.file=");
        AddConfiguration(arguments, "maintenance.auto=false");
        AddConfiguration(arguments, "gc.auto=0");
        AddConfiguration(arguments, "fetch.writeCommitGraph=false");
    }

    private static void AddConfiguration(Collection<string> arguments, string value)
    {
        arguments.Add("-c");
        arguments.Add(value);
    }

    internal static void ConfigureEnvironment(IDictionary<string, string?> environment)
    {
        RemoveHostGitOverrides(environment);
        environment["GIT_OPTIONAL_LOCKS"] = "0";
        environment["GIT_NO_LAZY_FETCH"] = "1";
        environment["GIT_TERMINAL_PROMPT"] = "0";
        environment["GIT_PAGER"] = "cat";
        environment["GIT_PROTOCOL_FROM_USER"] = "0";
        environment["GIT_LFS_SKIP_SMUDGE"] = "1";
        environment["GIT_ATTR_NOSYSTEM"] = "1";
        environment["GCM_INTERACTIVE"] = "Never";
        environment["SSH_ASKPASS_REQUIRE"] = "never";
        environment["LC_ALL"] = "C";
    }

    internal static void RemoveHostGitOverrides(IDictionary<string, string?> environment)
    {
        var exactNames = new[]
        {
            "GIT_ALTERNATE_OBJECT_DIRECTORIES", "GIT_ASKPASS", "GIT_COMMON_DIR",
            "GIT_CONFIG", "GIT_CONFIG_COUNT", "GIT_CONFIG_GLOBAL", "GIT_CONFIG_NOSYSTEM",
            "GIT_CONFIG_PARAMETERS", "GIT_CONFIG_SYSTEM", "GIT_DIR",
            "GIT_EXTERNAL_DIFF", "GIT_INDEX_FILE", "GIT_OBJECT_DIRECTORY", "GIT_SSH",
            "GIT_SSH_COMMAND", "GIT_WORK_TREE", "SSH_ASKPASS",
        };
        foreach (var name in exactNames)
        {
            environment.Remove(name);
        }

        foreach (var name in environment.Keys.Where(IsInjectedConfiguration).ToArray())
        {
            environment.Remove(name);
        }
    }

    private static bool IsInjectedConfiguration(string name) =>
        name.StartsWith("GIT_CONFIG_KEY_", StringComparison.OrdinalIgnoreCase)
        || name.StartsWith("GIT_CONFIG_VALUE_", StringComparison.OrdinalIgnoreCase)
        || name.StartsWith("GIT_TRACE", StringComparison.OrdinalIgnoreCase);

    private static void Kill(DiagnosticsProcess process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
        }
        catch (InvalidOperationException)
        {
            return;
        }

        if (!process.HasExited)
        {
            process.WaitForExit();
        }
    }

    internal sealed class OutputBudget(int maximumBytes)
    {
        private int _remainingBytes = maximumBytes;

        public void Consume(int byteCount)
        {
            if (Interlocked.Add(ref _remainingBytes, -byteCount) >= 0)
            {
                return;
            }

            throw new GitProcessException(
                RepositoryErrorCode.MalformedOutput,
                "La sortie Git dépasse la limite autorisée.");
        }
    }
}
