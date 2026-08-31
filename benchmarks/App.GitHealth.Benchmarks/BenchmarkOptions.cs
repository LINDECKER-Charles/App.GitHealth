using System.Globalization;

namespace App.GitHealth.Benchmarks;

internal sealed record BenchmarkOptions
{
    public required IReadOnlyList<int> BranchCounts { get; init; }

    public required int MeasurementIterations { get; init; }

    public required int WarmupIterations { get; init; }

    public required string OutputPath { get; init; }

    public required string BudgetsPath { get; init; }

    public required bool EnforceBudgets { get; init; }
}

internal sealed record BenchmarkOptionsParseResult(
    BenchmarkOptions? Options,
    bool ShowHelp);

internal sealed class BenchmarkOptionException(string message) : Exception(message);

internal static class BenchmarkOptionsParser
{
    public const string HelpText = """
        Reproducible GitHealth benchmark

        Usage:
          dotnet run --project benchmarks/App.GitHealth.Benchmarks -c Release -- [options]

        Options:
          --sizes <list>        Branch counts, comma-separated
                                (default: 100,500,1000)
          --iterations <n>      Measurements kept per phase (default: 3)
          --warmup <n>          Warm-up iterations per phase (default: 1)
          --output <path>       JSON report produced
          --budgets <path>      JSON budgets to compare against if the file exists
          --enforce-budgets     Return exit code 2 when a budget is exceeded
          --help                Print this help
        """;

    public static BenchmarkOptionsParseResult Parse(IReadOnlyList<string> args)
    {
        var parsed = ReadArguments(args);
        return parsed.ShowHelp
            ? new BenchmarkOptionsParseResult(null, ShowHelp: true)
            : new BenchmarkOptionsParseResult(
                CreateOptions(parsed.Values, parsed.EnforceBudgets),
                ShowHelp: false);
    }

    private static ParsedArguments ReadArguments(IReadOnlyList<string> args)
    {
        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        var shouldEnforce = false;
        for (var index = 0; index < args.Count; index++)
        {
            var argument = args[index];
            if (argument is "--help" or "-h")
            {
                return new ParsedArguments(values, shouldEnforce, ShowHelp: true);
            }

            if (argument == "--enforce-budgets")
            {
                shouldEnforce = true;
                continue;
            }

            index = ReadValue(args, index, values);
        }

        return new ParsedArguments(values, shouldEnforce, ShowHelp: false);
    }

    private static int ReadValue(
        IReadOnlyList<string> args,
        int optionIndex,
        Dictionary<string, string> values)
    {
        var argument = args[optionIndex];
        if (!KnownValueOptions.Contains(argument, StringComparer.Ordinal))
        {
            throw new BenchmarkOptionException($"Unknown option: {argument}");
        }

        var valueIndex = optionIndex + 1;
        if (valueIndex >= args.Count)
        {
            throw new BenchmarkOptionException($"Missing value for {argument}.");
        }

        values[argument] = args[valueIndex];
        return valueIndex;
    }

    private static BenchmarkOptions CreateOptions(
        IReadOnlyDictionary<string, string> values,
        bool enforceBudgets)
    {
        var currentDirectory = Environment.CurrentDirectory;
        return new BenchmarkOptions
        {
            BranchCounts = ParseBranchCounts(Get(values, "--sizes", "100,500,1000")),
            MeasurementIterations = ParsePositive(
                Get(values, "--iterations", "3"),
                "--iterations"),
            WarmupIterations = ParseNonNegative(Get(values, "--warmup", "1"), "--warmup"),
            OutputPath = ResolvePath(
                currentDirectory,
                Get(values, "--output", "artifacts/benchmarks/latest.json")),
            BudgetsPath = ResolvePath(
                currentDirectory,
                Get(values, "--budgets", "benchmarks/budgets.json")),
            EnforceBudgets = enforceBudgets,
        };
    }

    private static int[] ParseBranchCounts(string value)
    {
        var counts = value.Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(item => ParsePositive(item.Trim(), "--sizes"))
            .Distinct()
            .Order()
            .ToArray();
        if (counts.Length == 0 || counts.Any(count => count > 10_000))
        {
            throw new BenchmarkOptionException(
                "--sizes must contain integers between 1 and 10000.");
        }

        return counts;
    }

    private static int ParsePositive(string value, string option)
    {
        var parsed = ParseNonNegative(value, option);
        return parsed > 0
            ? parsed
            : throw new BenchmarkOptionException($"{option} must be greater than zero.");
    }

    private static int ParseNonNegative(string value, string option)
    {
        return int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var parsed)
            && parsed >= 0
            ? parsed
            : throw new BenchmarkOptionException($"Invalid value for {option}: {value}");
    }

    private static string Get(
        IReadOnlyDictionary<string, string> values,
        string key,
        string fallback) => values.GetValueOrDefault(key, fallback);

    private static string ResolvePath(string root, string path) =>
        Path.GetFullPath(Path.IsPathRooted(path) ? path : Path.Combine(root, path));

    private static readonly string[] KnownValueOptions =
    [
        "--sizes",
        "--iterations",
        "--warmup",
        "--output",
        "--budgets",
    ];

    private sealed record ParsedArguments(
        IReadOnlyDictionary<string, string> Values,
        bool EnforceBudgets,
        bool ShowHelp);
}
