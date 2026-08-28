using System.Diagnostics;
using System.Text;
using App.GitHealth.Core.Analysis;
using Microsoft.Extensions.Options;
using DiagnosticsProcess = System.Diagnostics.Process;

namespace App.GitHealth.Api.Git.Process;

internal sealed class GitProcessRunner(IOptions<GitScannerOptions> options) : IGitProcessRunner
{
    private readonly GitScannerOptions _options = options.Value;

    public async Task<GitCommandResult> RunAsync(
        GitCommand command,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var process = StartProcess(command);
        process.StandardInput.Close();
        return await MonitorAsync(process, cancellationToken);
    }

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
        var outputTask = ReadBoundedAsync(process.StandardOutput, cancellationToken);
        var errorTask = ReadBoundedAsync(process.StandardError, cancellationToken);
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

    private static ProcessStartInfo CreateStartInfo(GitCommand command)
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

        startInfo.ArgumentList.Add("--no-pager");
        startInfo.ArgumentList.Add("-c");
        startInfo.ArgumentList.Add("color.ui=false");
        foreach (var argument in command.Arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        ConfigureEnvironment(startInfo.Environment);
        return startInfo;
    }

    private async Task<string> ReadBoundedAsync(
        StreamReader reader,
        CancellationToken cancellationToken)
    {
        var buffer = new char[4096];
        var content = new StringBuilder();
        while (true)
        {
            var count = await reader.ReadAsync(buffer, cancellationToken);
            if (count == 0)
            {
                return content.ToString();
            }

            if (content.Length + count > _options.MaximumOutputCharacters)
            {
                throw new GitProcessException(
                    RepositoryErrorCode.MalformedOutput,
                    "La sortie Git dépasse la limite autorisée.");
            }

            content.Append(buffer, 0, count);
        }
    }

    private static void ConfigureEnvironment(IDictionary<string, string?> environment)
    {
        environment["GIT_OPTIONAL_LOCKS"] = "0";
        environment["GIT_NO_LAZY_FETCH"] = "1";
        environment["GIT_TERMINAL_PROMPT"] = "0";
        environment["GIT_PAGER"] = "cat";
        environment["LC_ALL"] = "C";
    }

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
}
