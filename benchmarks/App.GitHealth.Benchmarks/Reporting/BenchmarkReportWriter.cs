using System.Text.Json;
using System.Text.Json.Serialization;

namespace App.GitHealth.Benchmarks.Reporting;

internal static class BenchmarkReportWriter
{
    private static readonly JsonSerializerOptions SerializerOptions = new(
        JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
    };

    public static async Task WriteAsync(BenchmarkReport report, string outputPath)
    {
        var directory = Path.GetDirectoryName(outputPath)
            ?? throw new InvalidOperationException("Invalid report path.");
        Directory.CreateDirectory(directory);
        await using var stream = File.Create(outputPath);
        await JsonSerializer.SerializeAsync(stream, report, SerializerOptions);
        await stream.FlushAsync();
    }
}

internal static class BenchmarkConsoleWriter
{
    public static void Write(BenchmarkReport report, string outputPath)
    {
        Console.WriteLine();
        Console.WriteLine("Branches | Phase         | Median (ms)  | P95 (ms) | Budget");
        Console.WriteLine("---------+---------------+--------------+----------+--------");
        foreach (var scenario in report.Scenarios)
        {
            foreach (var phase in scenario.Phases)
            {
                Console.WriteLine(
                    $"{scenario.BranchCount,8} | {phase.Name,-13} | " +
                    $"{phase.MedianMilliseconds,12:F2} | {phase.P95Milliseconds,8:F2} | " +
                    BudgetLabel(phase));
            }
        }

        Console.WriteLine();
        Console.WriteLine($"Report written to {outputPath}");
    }

    private static string BudgetLabel(BenchmarkPhaseResult phase) => phase.BudgetStatus switch
    {
        BudgetStatus.NotConfigured => "not configured",
        BudgetStatus.WithinBudget => $"OK <= {phase.MaximumP95Milliseconds:F0} ms",
        _ => $"EXCEEDED > {phase.MaximumP95Milliseconds:F0} ms",
    };
}
