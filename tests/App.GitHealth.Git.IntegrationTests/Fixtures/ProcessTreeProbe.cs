using System.Globalization;
using App.GitHealth.Api.Git.Process;
using DiagnosticsProcess = System.Diagnostics.Process;

namespace App.GitHealth.Git.IntegrationTests.Fixtures;

internal sealed class ProcessTreeProbe : IDisposable
{
    private readonly string _childPidPath;
    private readonly string _directory;
    private readonly string _parentPidPath;

    public ProcessTreeProbe()
    {
        _directory = Path.Combine(
            Path.GetTempPath(),
            "githealth-process",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_directory);
        _parentPidPath = Path.Combine(_directory, "parent.pid");
        _childPidPath = Path.Combine(_directory, "child.pid");
    }

    public GitCommand CreateCommand()
    {
        var hostPath = Path.Combine(
            AppContext.BaseDirectory,
            "App.GitHealth.Git.TestHost.dll");
        var alias = string.Join(
            ' ',
            "alias.wait=!dotnet \"" + Normalize(hostPath) + "\"",
            "spawn",
            "\"" + Normalize(_parentPidPath) + "\"",
            "\"" + Normalize(_childPidPath) + "\"");
        return GitCommand.Create(Environment.CurrentDirectory, ["-c", alias, "wait"]);
    }

    public async Task<(int Parent, int Child)> WaitForProcessesAsync(
        Task<GitCommandResult> execution)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        while (!File.Exists(_parentPidPath) || !File.Exists(_childPidPath))
        {
            if (execution.IsCompleted)
            {
                var result = await execution;
                throw new Xunit.Sdk.XunitException(
                    $"Git {result.ExitCode}: {result.StandardOutput} {result.StandardError}");
            }

            await Task.Delay(50, timeout.Token);
        }

        return (ReadPid(_parentPidPath), ReadPid(_childPidPath));
    }

    public static async Task AssertStoppedAsync(int parentPid, int childPid)
    {
        await WaitUntilStoppedAsync(parentPid);
        await WaitUntilStoppedAsync(childPid);
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    private static int ReadPid(string path) =>
        int.Parse(File.ReadAllText(path), CultureInfo.InvariantCulture);

    private static string Normalize(string path) => path.Replace('\\', '/');

    private static async Task WaitUntilStoppedAsync(int processId)
    {
        for (var attempt = 0; attempt < 40; attempt++)
        {
            try
            {
                using var process = DiagnosticsProcess.GetProcessById(processId);
                if (process.HasExited)
                {
                    return;
                }
            }
            catch (ArgumentException)
            {
                return;
            }

            await Task.Delay(50);
        }

        throw new Xunit.Sdk.XunitException($"Le processus {processId} est encore actif.");
    }
}
