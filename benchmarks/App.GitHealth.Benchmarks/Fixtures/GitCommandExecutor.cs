using System.Diagnostics;
using System.Text;
using App.GitHealth.Api.Git.Process;

namespace App.GitHealth.Benchmarks.Fixtures;

internal static class GitCommandExecutor
{
    public static async Task<string> RunAsync(
        GitCommandRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        using var process = Start(request);
        var outputTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var errorTask = process.StandardError.ReadToEndAsync(cancellationToken);
        var inputException = await WriteInputAsync(
            process,
            request.StandardInput,
            cancellationToken);
        await process.WaitForExitAsync(cancellationToken);
        var result = new GitCommandExecutionResult(
            process.ExitCode,
            await outputTask,
            await errorTask)
        {
            InputException = inputException,
        };
        return EnsureSucceeded(request, result);
    }

    private static Process Start(GitCommandRequest request)
    {
        var process = new Process
        {
            StartInfo = CreateStartInfo(request.WorkingDirectory, request.Arguments),
        };
        if (process.Start())
        {
            return process;
        }

        process.Dispose();
        throw new InvalidOperationException("Git could not be started.");
    }

    private static async Task<IOException?> WriteInputAsync(
        Process process,
        string? standardInput,
        CancellationToken cancellationToken)
    {
        try
        {
            if (standardInput is not null)
            {
                await process.StandardInput.WriteAsync(
                    standardInput.AsMemory(),
                    cancellationToken);
            }
        }
        catch (IOException exception)
        {
            return exception;
        }
        finally
        {
            process.StandardInput.Close();
        }

        return null;
    }

    private static string EnsureSucceeded(
        GitCommandRequest request,
        GitCommandExecutionResult result)
    {
        if (result.ExitCode != 0)
        {
            throw new InvalidOperationException(
                $"Git {string.Join(' ', request.Arguments)} failed: {result.Error.Trim()}");
        }

        if (result.InputException is not null)
        {
            throw new InvalidOperationException(
                "Git closed its standard input prematurely.",
                result.InputException);
        }

        return result.Output;
    }

    public static Task<string> RunAsync(
        string workingDirectory,
        IReadOnlyList<string> arguments,
        CancellationToken cancellationToken) =>
        RunAsync(new GitCommandRequest(workingDirectory, arguments), cancellationToken);

    private static ProcessStartInfo CreateStartInfo(
        string workingDirectory,
        IReadOnlyList<string> arguments)
    {
        var startInfo = new ProcessStartInfo("git")
        {
            WorkingDirectory = workingDirectory,
            UseShellExecute = false,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardInputEncoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
            CreateNoWindow = true,
        };
        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        ConfigureEnvironment(startInfo.Environment);
        return startInfo;
    }

    internal static void ConfigureEnvironment(IDictionary<string, string?> environment)
    {
        GitProcessRunner.ConfigureEnvironment(environment);
        environment["GIT_CONFIG_GLOBAL"] = OperatingSystem.IsWindows()
            ? "NUL"
            : "/dev/null";
        environment["GIT_CONFIG_NOSYSTEM"] = "1";
    }

    private sealed record GitCommandExecutionResult(
        int ExitCode,
        string Output,
        string Error)
    {
        public IOException? InputException { get; init; }
    }
}
