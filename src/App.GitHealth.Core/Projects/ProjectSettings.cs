using App.GitHealth.Core.Branches;

namespace App.GitHealth.Core.Projects;

public sealed record ProjectSettings
{
    public const int MaximumBranchNamespaceLength = 512;
    private string _branchNamespace = "refs/heads/*";

    public static ProjectSettings Default { get; } = new();

    public GitRef? Reference { get; init; }

    public string BranchNamespace
    {
        get => _branchNamespace;
        init
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(value);
            if (value.Length > MaximumBranchNamespaceLength)
            {
                throw new ArgumentException(
                    $"Le périmètre de branches ne peut pas dépasser "
                    + $"{MaximumBranchNamespaceLength} caractères.",
                    nameof(value));
            }

            _branchNamespace = value.Trim();
        }
    }

    public ActivityThresholds Thresholds { get; init; } = ActivityThresholds.Default;

    public BranchPolicy Policy { get; init; } = BranchPolicy.Empty;
}
