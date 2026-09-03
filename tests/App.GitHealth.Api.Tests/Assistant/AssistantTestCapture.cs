using App.GitHealth.Core.Assistant;

namespace App.GitHealth.Api.Tests.Assistant;

/// <summary>
/// A capture built by hand rather than scanned, so the bridge can be exercised against known
/// rows. Five branches spread over two authors, three verdicts and both flags, with two more
/// measured but left out by the size cap.
/// </summary>
internal static class AssistantTestCapture
{
    public const int OmittedBranches = 2;

    private const string Baseline = "refs/heads/main";
    private const string FirstAuthor = "Ada Lovelace";
    private const string SecondAuthor = "Grace Hopper";

    private static readonly DateTimeOffset CapturedAt =
        new(2026, 9, 2, 10, 0, 0, TimeSpan.Zero);

    public static AnalysisBriefing Create() => new()
    {
        RepositoryName = "Storefront",
        Baseline = Baseline,
        CapturedAt = CapturedAt,
        Policy = new BriefingPolicy
        {
            ActiveUntilDays = 30,
            InactiveAfterDays = 90,
            ProtectedPatterns = ["refs/heads/release/*"],
            ExcludedPatterns = ["refs/heads/tmp/*"],
        },
        Branches = Branches(),
        OmittedBranchCount = OmittedBranches,
    };

    private static IReadOnlyList<BriefingBranch> Branches() =>
    [
        new BriefingBranch
        {
            ReferenceName = "refs/heads/feature/reporting",
            AheadCount = 4,
            BehindCount = 2,
            Relationship = "CommonAncestor",
            Topology = "Diverged",
            Activity = "Active",
            Recommendation = "Review",
            Reason = "Ahead of the baseline and still moving.",
            LastActivityAt = CapturedAt.AddDays(-3),
            TipAuthor = FirstAuthor,
        },
        new BriefingBranch
        {
            ReferenceName = "refs/heads/feature/checkout",
            AheadCount = 0,
            BehindCount = 6,
            Relationship = "Ancestor",
            Topology = "Merged",
            Activity = "Inactive",
            Recommendation = "CleanupCandidate",
            Reason = "Fully merged and untouched for months.",
            LastActivityAt = CapturedAt.AddDays(-200),
            TipAuthor = SecondAuthor,
        },
        new BriefingBranch
        {
            ReferenceName = "refs/heads/release/2026-08",
            AheadCount = 3,
            BehindCount = 0,
            Relationship = "Descendant",
            Topology = "Ahead",
            Activity = "Aging",
            Recommendation = "Keep",
            Reason = "Shielded by the policy.",
            LastActivityAt = CapturedAt.AddDays(-45),
            TipAuthor = FirstAuthor,
            IsProtected = true,
        },
        new BriefingBranch
        {
            ReferenceName = "refs/heads/tmp/spike",
            AheadCount = 1,
            BehindCount = 0,
            Relationship = "Descendant",
            Topology = "Ahead",
            Activity = "Inactive",
            Recommendation = "Excluded",
            Reason = "Left out of the reading by the policy.",
            IsExcluded = true,
        },
        new BriefingBranch
        {
            ReferenceName = "refs/heads/hotfix/login",
            AheadCount = 0,
            BehindCount = 9,
            Relationship = "Ancestor",
            Topology = "Merged",
            Activity = "Inactive",
            Recommendation = "CleanupCandidate",
            Reason = "Fully merged and untouched for months.",
            LastActivityAt = CapturedAt.AddDays(-150),
            TipAuthor = SecondAuthor,
        },
    ];
}
