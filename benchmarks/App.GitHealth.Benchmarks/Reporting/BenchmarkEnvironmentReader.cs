using System.Runtime.InteropServices;
using App.GitHealth.Benchmarks.Fixtures;

namespace App.GitHealth.Benchmarks.Reporting;

internal static class BenchmarkEnvironmentReader
{
    public static async Task<BenchmarkEnvironment> ReadAsync(
        CancellationToken cancellationToken)
    {
        var workingDirectory = Environment.CurrentDirectory;
        var gitVersion = await GitCommandExecutor.RunAsync(
            workingDirectory,
            ["--version"],
            cancellationToken);
        var sourceCommit = await GitCommandExecutor.RunAsync(
            workingDirectory,
            ["rev-parse", "HEAD"],
            cancellationToken);
        var status = await GitCommandExecutor.RunAsync(
            workingDirectory,
            ["status", "--porcelain"],
            cancellationToken);

        return new BenchmarkEnvironment
        {
            OperatingSystem = RuntimeInformation.OSDescription,
            RuntimeIdentifier = RuntimeInformation.RuntimeIdentifier,
            ProcessArchitecture = RuntimeInformation.ProcessArchitecture.ToString(),
            Framework = RuntimeInformation.FrameworkDescription,
            Processor = ReadProcessorDescription(),
            LogicalProcessorCount = Environment.ProcessorCount,
            GitVersion = gitVersion.Trim(),
            SourceCommit = sourceCommit.Trim(),
            SourceWorkingTreeDirty = !string.IsNullOrWhiteSpace(status),
        };
    }

    private static string ReadProcessorDescription() =>
        Environment.GetEnvironmentVariable("PROCESSOR_IDENTIFIER")
        ?? Environment.GetEnvironmentVariable("HOSTTYPE")
        ?? "non renseigné";
}
