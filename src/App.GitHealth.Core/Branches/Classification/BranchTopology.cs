namespace App.GitHealth.Core.Branches;

public enum BranchTopology
{
    Synchronized,
    Ahead,
    Merged,
    Diverged,
    Unrelated,
}
