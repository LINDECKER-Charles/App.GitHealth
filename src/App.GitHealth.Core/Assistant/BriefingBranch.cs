namespace App.GitHealth.Core.Assistant;

/// <summary>
/// One measured branch, reduced to the facts an agent can reason about. Nothing here is
/// read from the repository at briefing time: it all comes from a capture already taken.
/// </summary>
public sealed record BriefingBranch
{
    public required string ReferenceName { get; init; }

    public required int AheadCount { get; init; }

    public required int BehindCount { get; init; }

    public required string Relationship { get; init; }

    public required string Topology { get; init; }

    public required string Activity { get; init; }

    /// <summary>Verdict GitHealth reached on its own, which the agent may argue with.</summary>
    public required string Recommendation { get; init; }

    public required string Reason { get; init; }

    public DateTimeOffset? LastActivityAt { get; init; }

    /// <summary>
    /// Author of the tip commit, by display name. The address is deliberately absent: it
    /// identifies a person without helping the reading, and this text leaves the machine.
    /// </summary>
    public string? TipAuthor { get; init; }

    public bool IsProtected { get; init; }

    public bool IsExcluded { get; init; }
}
