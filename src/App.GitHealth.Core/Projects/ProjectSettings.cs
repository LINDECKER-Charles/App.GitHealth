using App.GitHealth.Core.Branches;

namespace App.GitHealth.Core.Projects;

public sealed record ProjectSettings
{
    public const int MaximumBranchNamespaceLength = 512;

    /// <summary>
    /// Upper bound on comparison baselines. Each one costs a full scan of the repository,
    /// so the limit is what keeps a single "run an analysis" click bounded in time.
    /// </summary>
    public const int MaximumBaselineCount = 8;

    private readonly GitRef[] _baselines = [];
    private string _branchNamespace = "refs/heads/*";

    public static ProjectSettings Default { get; } = new();

    /// <summary>Comparison baselines, in display order. The first one is the primary.</summary>
    public IReadOnlyList<GitRef> Baselines
    {
        get => _baselines;
        init
        {
            ArgumentNullException.ThrowIfNull(value);
            _baselines = Validate(value);
        }
    }

    /// <summary>
    /// Primary baseline: what a reader that knows nothing of the list still sees. It is the
    /// baseline a project falls back to when no other one is asked for.
    /// </summary>
    public GitRef? Reference => _baselines.Length == 0 ? null : _baselines[0];

    public string BranchNamespace
    {
        get => _branchNamespace;
        init
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(value);
            if (value.Length > MaximumBranchNamespaceLength)
            {
                throw new ArgumentException(
                    $"The branch scope cannot exceed "
                    + $"{MaximumBranchNamespaceLength} characters.",
                    nameof(value));
            }

            _branchNamespace = value.Trim();
        }
    }

    public ActivityThresholds Thresholds { get; init; } = ActivityThresholds.Default;

    public BranchPolicy Policy { get; init; } = BranchPolicy.Empty;

    private static GitRef[] Validate(IReadOnlyList<GitRef> baselines)
    {
        if (baselines.Count > MaximumBaselineCount)
        {
            throw new ArgumentException(
                $"A project cannot declare more than {MaximumBaselineCount} baselines.",
                nameof(baselines));
        }

        var distinct = baselines
            .DistinctBy(baseline => baseline.FullName, StringComparer.Ordinal)
            .ToArray();
        return distinct.Length == baselines.Count
            ? distinct
            : throw new ArgumentException("A baseline is listed twice.", nameof(baselines));
    }
}
