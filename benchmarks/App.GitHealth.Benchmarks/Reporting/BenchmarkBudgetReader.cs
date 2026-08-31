using System.Text.Json;

namespace App.GitHealth.Benchmarks.Reporting;

internal sealed class BenchmarkBudgetReader
{
    private static readonly JsonSerializerOptions SerializerOptions =
        new(JsonSerializerDefaults.Web);
    private readonly IReadOnlyDictionary<(int BranchCount, string Phase), double> _budgets;

    private BenchmarkBudgetReader(
        IReadOnlyDictionary<(int BranchCount, string Phase), double> budgets,
        bool isLoaded)
    {
        _budgets = budgets;
        IsLoaded = isLoaded;
    }

    public bool IsLoaded { get; }

    public static async Task<BenchmarkBudgetReader> LoadAsync(
        string path,
        CancellationToken cancellationToken)
    {
        if (!File.Exists(path))
        {
            return new BenchmarkBudgetReader(
                new Dictionary<(int BranchCount, string Phase), double>(),
                isLoaded: false);
        }

        var document = await ReadDocumentAsync(path, cancellationToken);
        if (document.SchemaVersion != 1)
        {
            throw new InvalidOperationException(
                $"Unsupported budgets version: {document.SchemaVersion}.");
        }

        return new BenchmarkBudgetReader(CreateLookup(document.Budgets), isLoaded: true);
    }

    private static async Task<BenchmarkBudgetDocument> ReadDocumentAsync(
        string path,
        CancellationToken cancellationToken)
    {
        await using var stream = File.OpenRead(path);
        return await JsonSerializer.DeserializeAsync<BenchmarkBudgetDocument>(
            stream,
            SerializerOptions,
            cancellationToken)
            ?? throw new InvalidOperationException("The budgets file is empty.");
    }

    private static Dictionary<(int BranchCount, string Phase), double> CreateLookup(
        IReadOnlyList<BenchmarkBudget> configuredBudgets)
    {
        var budgets = new Dictionary<(int BranchCount, string Phase), double>();
        foreach (var budget in configuredBudgets)
        {
            Validate(budget);
            var key = (budget.BranchCount, budget.Phase.ToLowerInvariant());
            if (!budgets.TryAdd(key, budget.MaximumP95Milliseconds))
            {
                throw new InvalidOperationException(
                    $"Duplicate budget for {budget.BranchCount}/{budget.Phase}.");
            }
        }

        return budgets;
    }

    public double? Find(int branchCount, string phase)
    {
        return _budgets.TryGetValue(
            (branchCount, phase.ToLowerInvariant()),
            out var maximum)
            ? maximum
            : null;
    }

    private static void Validate(BenchmarkBudget budget)
    {
        if (budget.BranchCount <= 0
            || string.IsNullOrWhiteSpace(budget.Phase)
            || budget.MaximumP95Milliseconds <= 0)
        {
            throw new InvalidOperationException("A budget entry is invalid.");
        }
    }
}
