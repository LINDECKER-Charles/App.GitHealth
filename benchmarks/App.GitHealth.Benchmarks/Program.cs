using App.GitHealth.Benchmarks.Reporting;

namespace App.GitHealth.Benchmarks;

internal static class Program
{
    public static async Task<int> Main(string[] args)
    {
        try
        {
            var parseResult = BenchmarkOptionsParser.Parse(args);
            if (parseResult.ShowHelp)
            {
                Console.WriteLine(BenchmarkOptionsParser.HelpText);
                return 0;
            }

            var options = parseResult.Options!;
            var report = await new BenchmarkRunner(options).RunAsync(CancellationToken.None);
            await BenchmarkReportWriter.WriteAsync(report, options.OutputPath);
            BenchmarkConsoleWriter.Write(report, options.OutputPath);

            return options.EnforceBudgets && report.HasBudgetRegression ? 2 : 0;
        }
        catch (BenchmarkOptionException exception)
        {
            Console.Error.WriteLine(exception.Message);
            Console.Error.WriteLine(BenchmarkOptionsParser.HelpText);
            return 1;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine($"Benchmark interrompu : {exception.Message}");
            return 1;
        }
    }
}
