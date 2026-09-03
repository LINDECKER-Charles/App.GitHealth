namespace App.GitHealth.Core.Assistant;

/// <summary>
/// Everything an agent is given about a repository, and the only thing it is given: the
/// briefing is self-sufficient by design, so answering never requires opening the
/// repository. What is not in here is not available to the agent.
/// </summary>
public sealed record AnalysisBriefing
{
    public required string RepositoryName { get; init; }

    public required string Baseline { get; init; }

    public required DateTimeOffset CapturedAt { get; init; }

    public required BriefingPolicy Policy { get; init; }

    public required IReadOnlyList<BriefingBranch> Branches { get; init; }

    /// <summary>
    /// Branches measured but left out by the size cap. Stated rather than hidden: an agent
    /// asked to count must know it is reading a truncated list.
    /// </summary>
    public int OmittedBranchCount { get; init; }

    public int MeasuredBranchCount => Branches.Count + OmittedBranchCount;
}
