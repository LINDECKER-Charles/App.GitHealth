using App.GitHealth.Api.Git;
using App.GitHealth.Api.Git.Models;
using App.GitHealth.Api.Git.Process;
using App.GitHealth.Api.Git.Scanning;
using App.GitHealth.Core.Branches;
using App.GitHealth.Git.IntegrationTests.Fixtures;

namespace App.GitHealth.Git.IntegrationTests;

public sealed class GitTopologyReaderTests
{
    [Fact]
    public async Task UnsupportedAheadBehindAtomAutomaticallyUsesRevList()
    {
        var runner = new RecordingGitRunner();
        runner.Enqueue(new GitCommandResult(128, "", "unknown field name: ahead-behind"));
        runner.Enqueue(new GitCommandResult(0, "0\t1\n", ""));
        var reference = Capture("refs/heads/main", "aaaaaa");
        var branch = Capture("refs/heads/feature", "bbbbbb");
        var path = Environment.CurrentDirectory;
        var context = new GitRepositoryContext(path, path, path);
        var repository = new CapturedRepository(
            context,
            "git version 2.53.0",
            new Dictionary<string, CapturedReference>());
        var scan = new TopologyScan(repository, reference, [branch]);

        var result = await new GitTopologyReader(runner, new GitScannerOptions())
            .ReadAsync(scan, default);

        Assert.Equal(
            BranchDivergence.Create(1, 0, BranchRelationship.CommonAncestor),
            result[branch.Reference.FullName]);
        Assert.Contains("for-each-ref", runner.Commands[0].Arguments);
        Assert.Contains("rev-list", runner.Commands[1].Arguments);
    }

    private static CapturedReference Capture(string fullName, string commit)
    {
        var tip = new BranchTip(new CommitId(commit), DateTimeOffset.UnixEpoch, "Ada");
        return new CapturedReference(new GitRef(fullName), tip, null);
    }
}
