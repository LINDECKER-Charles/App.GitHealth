using System.Diagnostics;
using App.GitHealth.Benchmarks.Fixtures;
using App.GitHealth.Benchmarks.Phases;
using App.GitHealth.Benchmarks.Reporting;
using App.GitHealth.Core.Analysis;

namespace App.GitHealth.Benchmarks;

internal sealed class BenchmarkRunner(BenchmarkOptions options)
{
    public async Task<BenchmarkReport> RunAsync(CancellationToken cancellationToken)
    {
        var budgets = await BenchmarkBudgetReader.LoadAsync(
            options.BudgetsPath,
            cancellationToken);
        var environment = await BenchmarkEnvironmentReader.ReadAsync(cancellationToken);
        var scenarios = await RunScenariosAsync(budgets, cancellationToken);
        return CreateReport(environment, budgets, scenarios);
    }

    private async Task<IReadOnlyList<BenchmarkScenarioResult>> RunScenariosAsync(
        BenchmarkBudgetReader budgets,
        CancellationToken cancellationToken)
    {
        var scenarios = new List<BenchmarkScenarioResult>();
        foreach (var branchCount in options.BranchCounts)
        {
            scenarios.Add(await RunScenarioAsync(branchCount, budgets, cancellationToken));
        }

        return scenarios;
    }

    private BenchmarkReport CreateReport(
        BenchmarkEnvironment environment,
        BenchmarkBudgetReader budgets,
        IReadOnlyList<BenchmarkScenarioResult> scenarios) => new()
        {
            SchemaVersion = 1,
            GeneratedAtUtc = DateTimeOffset.UtcNow,
            Environment = environment,
            Configuration = new BenchmarkConfiguration
            {
                MeasurementIterations = options.MeasurementIterations,
                WarmupIterations = options.WarmupIterations,
                BudgetsPath = Path.GetRelativePath(
                    Environment.CurrentDirectory,
                    options.BudgetsPath),
                BudgetsLoaded = budgets.IsLoaded,
            },
            Scenarios = scenarios,
            HasBudgetRegression = scenarios
            .SelectMany(scenario => scenario.Phases)
            .Any(phase => phase.BudgetStatus == BudgetStatus.Exceeded),
        };

    private async Task<BenchmarkScenarioResult> RunScenarioAsync(
        int branchCount,
        BenchmarkBudgetReader budgets,
        CancellationToken cancellationToken)
    {
        Console.WriteLine($"Préparation de la fixture déterministe ({branchCount} branches)...");
        await using var fixture = await SyntheticGitFixture.CreateAsync(
            branchCount,
            cancellationToken);
        var measurements = await MeasurePhasesAsync(
            fixture.RepositoryPath,
            branchCount,
            cancellationToken);

        return new BenchmarkScenarioResult
        {
            BranchCount = branchCount,
            FixtureFingerprint = fixture.Fingerprint,
            Phases = measurements
                .Select(phase => CreatePhase(branchCount, phase, budgets))
                .ToArray(),
        };
    }

    private async Task<IReadOnlyList<PhaseMeasurement>> MeasurePhasesAsync(
        string repositoryPath,
        int branchCount,
        CancellationToken cancellationToken)
    {
        var git = await MeasureGitAsync(repositoryPath, branchCount, cancellationToken);
        var persistence = await MeasurePersistenceAsync(
            repositoryPath,
            git.Scan,
            cancellationToken);
        var api = await MeasureApiAsync(repositoryPath, git.Scan, cancellationToken);
        return
        [
            git.Topology,
            git.Enrichment,
            new PhaseMeasurement("persistence", persistence.Samples),
            new PhaseMeasurement("api", api.Samples),
        ];
    }

    private async Task<GitMeasurements> MeasureGitAsync(
        string repositoryPath,
        int branchCount,
        CancellationToken cancellationToken)
    {
        var benchmark = await GitPhaseBenchmark.CreateAsync(
            repositoryPath,
            branchCount,
            cancellationToken);
        var topology = await MeasureAsync(
            "topology",
            benchmark.ReadTopologyAsync,
            cancellationToken);
        var enrichment = await MeasureAsync(
            "enrichment",
            token => benchmark.EnrichAsync(topology.LastResult, token),
            cancellationToken);
        return new GitMeasurements(
            new PhaseMeasurement("topology", topology.Samples),
            new PhaseMeasurement("enrichment", enrichment.Samples),
            enrichment.LastResult);
    }

    private async Task<Measurement<T>> MeasureAsync<T>(
        string phase,
        Func<CancellationToken, Task<T>> operation,
        CancellationToken cancellationToken)
    {
        Console.WriteLine($"  Mesure {phase}...");
        T? lastResult = default;
        for (var iteration = 0; iteration < options.WarmupIterations; iteration++)
        {
            lastResult = await operation(cancellationToken);
        }

        var samples = new List<double>(options.MeasurementIterations);
        for (var iteration = 0; iteration < options.MeasurementIterations; iteration++)
        {
            PrepareForMeasurement();
            var startedAt = Stopwatch.GetTimestamp();
            lastResult = await operation(cancellationToken);
            samples.Add(Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds);
        }

        return new Measurement<T>(samples, lastResult!);
    }

    private async Task<Measurement<Guid>> MeasurePersistenceAsync(
        string repositoryPath,
        RepositoryScan scan,
        CancellationToken cancellationToken)
    {
        Console.WriteLine("  Mesure persistence...");
        var samples = new List<double>(options.MeasurementIterations);
        Guid lastResult = default;
        var totalIterations = options.WarmupIterations + options.MeasurementIterations;
        for (var iteration = 0; iteration < totalIterations; iteration++)
        {
            await using var database = await PersistencePhaseBenchmark.CreateAsync(
                repositoryPath,
                cancellationToken);
            PrepareForMeasurement();
            var startedAt = Stopwatch.GetTimestamp();
            lastResult = await database.PersistAsync(scan, cancellationToken);
            var elapsed = Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds;
            if (iteration >= options.WarmupIterations)
            {
                samples.Add(elapsed);
            }
        }

        return new Measurement<Guid>(samples, lastResult);
    }

    private async Task<Measurement<int>> MeasureApiAsync(
        string repositoryPath,
        RepositoryScan scan,
        CancellationToken cancellationToken)
    {
        await using var database = await PersistencePhaseBenchmark.CreateAsync(
            repositoryPath,
            cancellationToken);
        var analysisId = await database.PersistAsync(scan, cancellationToken);
        var measurement = await MeasureAsync(
            "api",
            token => database.RenderApiPayloadAsync(analysisId, token),
            cancellationToken);
        if (measurement.LastResult <= 0)
        {
            throw new InvalidOperationException("Le rendu API a produit une réponse vide.");
        }

        return measurement;
    }

    private static BenchmarkPhaseResult CreatePhase(
        int branchCount,
        PhaseMeasurement phase,
        BenchmarkBudgetReader budgets)
    {
        var rounded = phase.Samples.Select(value => Math.Round(value, 3)).ToArray();
        var median = Math.Round(BenchmarkStatistics.Median(phase.Samples), 3);
        var p95 = Math.Round(BenchmarkStatistics.Percentile95(phase.Samples), 3);
        var maximum = budgets.Find(branchCount, phase.Name);
        var status = maximum is null
            ? BudgetStatus.NotConfigured
            : p95 <= maximum
                ? BudgetStatus.WithinBudget
                : BudgetStatus.Exceeded;
        return new BenchmarkPhaseResult
        {
            Name = phase.Name,
            SamplesMilliseconds = rounded,
            MedianMilliseconds = median,
            P95Milliseconds = p95,
            MaximumP95Milliseconds = maximum,
            BudgetStatus = status,
        };
    }

    private static void PrepareForMeasurement()
    {
        GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, blocking: true, compacting: true);
        GC.WaitForPendingFinalizers();
        GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, blocking: true, compacting: true);
    }

    private sealed record Measurement<T>(IReadOnlyList<double> Samples, T LastResult);

    private sealed record PhaseMeasurement(string Name, IReadOnlyList<double> Samples);

    private sealed record GitMeasurements(
        PhaseMeasurement Topology,
        PhaseMeasurement Enrichment,
        RepositoryScan Scan);
}

internal static class BenchmarkStatistics
{
    public static double Median(IReadOnlyList<double> values)
    {
        var sorted = Sort(values);
        var middle = sorted.Length / 2;
        return sorted.Length % 2 == 0
            ? (sorted[middle - 1] + sorted[middle]) / 2
            : sorted[middle];
    }

    public static double Percentile95(IReadOnlyList<double> values)
    {
        var sorted = Sort(values);
        var index = (int)Math.Ceiling(sorted.Length * 0.95) - 1;
        return sorted[index];
    }

    private static double[] Sort(IReadOnlyList<double> values)
    {
        if (values.Count == 0)
        {
            throw new ArgumentException("Au moins une mesure est requise.", nameof(values));
        }

        return values.Order().ToArray();
    }
}
