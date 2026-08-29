using System.Text.Json.Serialization;

namespace App.GitHealth.Benchmarks.Reporting;

internal sealed record BenchmarkReport
{
    public required int SchemaVersion { get; init; }

    public required DateTimeOffset GeneratedAtUtc { get; init; }

    public required BenchmarkEnvironment Environment { get; init; }

    public required BenchmarkConfiguration Configuration { get; init; }

    public required IReadOnlyList<BenchmarkScenarioResult> Scenarios { get; init; }

    public required bool HasBudgetRegression { get; init; }
}

internal sealed record BenchmarkEnvironment
{
    public required string OperatingSystem { get; init; }

    public required string RuntimeIdentifier { get; init; }

    public required string ProcessArchitecture { get; init; }

    public required string Framework { get; init; }

    public required string Processor { get; init; }

    public required int LogicalProcessorCount { get; init; }

    public required string GitVersion { get; init; }

    public required string SourceCommit { get; init; }

    public required bool SourceWorkingTreeDirty { get; init; }
}

internal sealed record BenchmarkConfiguration
{
    public required int MeasurementIterations { get; init; }

    public required int WarmupIterations { get; init; }

    public required string BudgetsPath { get; init; }

    public required bool BudgetsLoaded { get; init; }
}

internal sealed record BenchmarkScenarioResult
{
    public required int BranchCount { get; init; }

    public required string FixtureFingerprint { get; init; }

    public required IReadOnlyList<BenchmarkPhaseResult> Phases { get; init; }
}

internal sealed record BenchmarkPhaseResult
{
    public required string Name { get; init; }

    public required IReadOnlyList<double> SamplesMilliseconds { get; init; }

    public required double MedianMilliseconds { get; init; }

    public required double P95Milliseconds { get; init; }

    public double? MaximumP95Milliseconds { get; init; }

    [JsonConverter(typeof(JsonStringEnumConverter<BudgetStatus>))]
    public required BudgetStatus BudgetStatus { get; init; }
}

internal enum BudgetStatus
{
    NotConfigured,
    WithinBudget,
    Exceeded,
}

internal sealed record BenchmarkBudgetDocument
{
    public required int SchemaVersion { get; init; }

    public required string Policy { get; init; }

    public required IReadOnlyList<BenchmarkBudget> Budgets { get; init; }
}

internal sealed record BenchmarkBudget
{
    public required int BranchCount { get; init; }

    public required string Phase { get; init; }

    public required double MaximumP95Milliseconds { get; init; }
}
