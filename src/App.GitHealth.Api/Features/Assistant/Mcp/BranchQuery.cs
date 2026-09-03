using App.GitHealth.Core.Assistant;

namespace App.GitHealth.Api.Features.Assistant.Mcp;

/// <summary>
/// What the agent asked to see of a capture. Every filter is optional and every unknown
/// value simply matches nothing, so a mistyped verdict returns an empty page rather than
/// the whole table — the agent notices the difference, a silent fallback it would not.
/// </summary>
internal sealed record BranchQuery
{
    public const int DefaultTake = 50;
    public const int MaximumTake = 500;

    public string? Verdict { get; init; }

    public string? Topology { get; init; }

    public string? Activity { get; init; }

    /// <summary>Matched on a fragment: authors are typed by hand and rarely twice alike.</summary>
    public string? Author { get; init; }

    public string? NameContains { get; init; }

    public bool? IsProtected { get; init; }

    public bool? IsExcluded { get; init; }

    public int Skip { get; init; }

    public int Take { get; init; } = DefaultTake;

    public IReadOnlyList<BriefingBranch> Apply(IReadOnlyList<BriefingBranch> branches)
    {
        ArgumentNullException.ThrowIfNull(branches);
        return [.. branches
            .Where(Matches)
            .Skip(Math.Max(0, Skip))
            .Take(Math.Clamp(Take, 1, MaximumTake))];
    }

    public int Count(IReadOnlyList<BriefingBranch> branches)
    {
        ArgumentNullException.ThrowIfNull(branches);
        return branches.Count(Matches);
    }

    private bool Matches(BriefingBranch branch) =>
        Equals(Verdict, branch.Recommendation)
        && Equals(Topology, branch.Topology)
        && Equals(Activity, branch.Activity)
        && Contains(Author, branch.TipAuthor)
        && Contains(NameContains, branch.ReferenceName)
        && Flagged(IsProtected, branch.IsProtected)
        && Flagged(IsExcluded, branch.IsExcluded);

    private static bool Equals(string? wanted, string actual) =>
        string.IsNullOrEmpty(wanted)
        || string.Equals(wanted, actual, StringComparison.OrdinalIgnoreCase);

    private static bool Contains(string? fragment, string? actual) =>
        string.IsNullOrEmpty(fragment)
        || (actual is not null
            && actual.Contains(fragment, StringComparison.OrdinalIgnoreCase));

    private static bool Flagged(bool? wanted, bool actual) =>
        wanted is null || wanted.Value == actual;
}
