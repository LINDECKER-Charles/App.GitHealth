using App.GitHealth.Core.Branches;

namespace App.GitHealth.Core.Projects;

public sealed record ProjectSettings
{
    public static ProjectSettings Default { get; } = new();

    public GitRef? Reference { get; init; }

    public string BranchNamespace { get; init; } = "refs/heads/*";

    public ActivityThresholds Thresholds { get; init; } = ActivityThresholds.Default;

    public BranchPolicy Policy { get; init; } = BranchPolicy.Empty;
}
