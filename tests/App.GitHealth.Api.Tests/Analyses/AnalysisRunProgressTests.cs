using App.GitHealth.Api.Features.Analyses;
using App.GitHealth.Core.Analysis;
using App.GitHealth.Core.Branches;

namespace App.GitHealth.Api.Tests.Analyses;

public sealed class AnalysisRunProgressTests
{
    private static readonly DateTimeOffset ReadAt = new(2026, 9, 2, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public void LedgerKeepsTheOrderTheReferencesWereListedIn()
    {
        var progress = new AnalysisRunProgress();

        progress.List([Listing("refs/heads/b"), Listing("refs/heads/a")]);

        Assert.Equal(
            ["refs/heads/b", "refs/heads/a"],
            progress.Snapshot(ReadAt).References.Select(item => item.ReferenceName));
    }

    [Fact]
    public void ReferenceWalksFromListedToRead()
    {
        var progress = new AnalysisRunProgress();
        progress.List([Listing("refs/heads/a")]);

        Assert.Equal(ReferenceProgressState.Listed, State(progress));
        progress.Start("refs/heads/a", RepositoryScanStage.Topology);
        Assert.Equal(ReferenceProgressState.Measuring, State(progress));
        progress.Measure(Measured("refs/heads/a"));
        Assert.Equal(ReferenceProgressState.Measured, State(progress));
        progress.Start("refs/heads/a", RepositoryScanStage.Enrichment);
        Assert.Equal(ReferenceProgressState.Enriching, State(progress));
        progress.Enrich(Enriched("refs/heads/a"));
        Assert.Equal(ReferenceProgressState.Read, State(progress));
    }

    [Fact]
    public void MeasuringNamesTheTopologyAlongsideTheDistance()
    {
        var progress = new AnalysisRunProgress();
        progress.List([Listing("refs/heads/a")]);

        progress.Measure(Measured("refs/heads/a"));

        var reference = Assert.Single(progress.Snapshot(ReadAt).References);
        Assert.Equal(3, reference.AheadCount);
        Assert.Equal(2, reference.BehindCount);
        Assert.Equal(BranchTopology.Diverged, reference.Topology);
        Assert.Equal("c480b1a7", reference.MergeBaseCommit);
    }

    [Fact]
    public void EventsAboutAnUnknownReferenceAreIgnored()
    {
        var progress = new AnalysisRunProgress();
        progress.List([Listing("refs/heads/a")]);

        progress.Start("refs/heads/gone", RepositoryScanStage.Topology);
        progress.Measure(Measured("refs/heads/gone"));

        Assert.Equal(ReferenceProgressState.Listed, State(progress));
    }

    [Fact]
    public void CommandsAreRankedAndOnlyTheTailIsKept()
    {
        var progress = new AnalysisRunProgress();

        for (var index = 1; index <= 65; index += 1)
        {
            progress.Record(Command($"git merge-base main branch-{index}"));
        }

        var snapshot = progress.Snapshot(ReadAt);
        Assert.Equal(65, snapshot.CommandCount);
        Assert.Equal(60, snapshot.Commands.Count);
        Assert.Equal(6, snapshot.Commands[0].Sequence);
        Assert.Equal(65, snapshot.Commands[^1].Sequence);
        Assert.Equal("git merge-base main branch-65", snapshot.Commands[^1].CommandLine);
    }

    [Fact]
    public void SnapshotIsACopy()
    {
        var progress = new AnalysisRunProgress();
        progress.List([Listing("refs/heads/a")]);

        var before = progress.Snapshot(ReadAt);
        progress.Measure(Measured("refs/heads/a"));

        Assert.Equal(ReferenceProgressState.Listed, before.References[0].State);
    }

    private static ReferenceProgressState State(AnalysisRunProgress progress) =>
        progress.Snapshot(ReadAt).References[0].State;

    private static ScannedReferenceListing Listing(string referenceName) => new()
    {
        ReferenceName = referenceName,
        CommitId = "aaaaaaaabbbbbbbb",
        LastActivityAtUtc = ReadAt,
        TipAuthor = "Ada Lovelace",
    };

    private static ScanReferenceMeasured Measured(string referenceName) => new()
    {
        ReferenceName = referenceName,
        Divergence = BranchDivergence.Create(3, 2, BranchRelationship.CommonAncestor),
        MergeBaseCommit = "c480b1a7",
    };

    private static ScanReferenceEnriched Enriched(string referenceName) => new()
    {
        ReferenceName = referenceName,
        TopContributor = "Ada Lovelace",
        ContributorCount = 2,
    };

    private static ScanCommandCompleted Command(string commandLine) => new()
    {
        CommandLine = commandLine,
        Duration = TimeSpan.FromMilliseconds(4),
        ExitCode = 0,
        Output = "c480b1a7",
    };
}
