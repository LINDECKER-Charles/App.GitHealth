using App.GitHealth.Api.Persistence.Models;
using App.GitHealth.Core.Analysis;
using App.GitHealth.Core.Branches;
using App.GitHealth.Core.Projects;

namespace App.GitHealth.Api.Tests.Persistence;

internal static class PersistenceTestData
{
    public const string PrimaryBaseline = "refs/heads/main";
    public const string SecondaryBaseline = "refs/remotes/origin/main";

    private const string ReferenceCommit = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
    private const string BranchCommit = "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";

    public static AnalysisTarget PrimaryTarget(Guid projectId) =>
        new(projectId, PrimaryBaseline);

    public static AnalysisTarget SecondaryTarget(Guid projectId) =>
        new(projectId, SecondaryBaseline);

    public static Project CreateProject(string repositoryPath)
    {
        var settings = new ProjectSettings
        {
            Baselines = [new GitRef(PrimaryBaseline), new GitRef(SecondaryBaseline)],
            BranchNamespace = "refs/remotes/origin/*",
            Thresholds = ActivityThresholds.Create(14, 60),
            Policy = BranchPolicy.Create(["refs/heads/tmp/*"], ["refs/heads/release/*"]),
        };
        return Project.Create("Test repository", repositoryPath) with { Settings = settings };
    }

    public static RepositoryScan CreateScan(
        DateTimeOffset capturedAtUtc,
        params string[] branchReferences)
    {
        var branches = branchReferences.Length == 0
            ? [CreateBranch("refs/remotes/origin/feature/café", capturedAtUtc)]
            : branchReferences.Select(reference => CreateBranch(reference, capturedAtUtc));
        return new RepositoryScan(
            new RepositoryScanMetadata(capturedAtUtc, "git version 2.51.0"),
            new CommitId(ReferenceCommit),
            branches);
    }

    private static ScannedBranch CreateBranch(
        string referenceName,
        DateTimeOffset capturedAtUtc)
    {
        var divergence = BranchDivergence.Create(2, 1, BranchRelationship.CommonAncestor);
        var tip = new BranchTip(
            new CommitId(BranchCommit),
            capturedAtUtc.AddDays(-2),
            "Ada Lovelace <ada@example.test>");
        var facts = new BranchFacts(new GitRef(referenceName), divergence, tip);
        return new ScannedBranch(facts, [new Contributor("Ada Lovelace", "ada@example.test", 2)]);
    }
}
