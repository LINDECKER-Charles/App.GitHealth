using App.GitHealth.Core.Analysis;
using App.GitHealth.Core.Branches;
using App.GitHealth.Git.IntegrationTests.Fixtures;

namespace App.GitHealth.Git.IntegrationTests;

/// <summary>
/// A followed scan must narrate the very repository it reads: the events are what the
/// interface shows while it waits, so they cannot drift from the result.
/// </summary>
public sealed class ScanProgressTests
{
    private const string Baseline = "refs/heads/main";

    [Fact]
    public async Task FollowedScanAnnouncesEveryReferenceItReads()
    {
        using var repository = GitTestRepository.Create();
        var progress = new RecordingScanProgress();
        var scanner = GitScannerFactory.Create();

        var result = await scanner.ScanAsync(Request(repository), progress, default);

        Assert.True(result.TryGetValue(out var scan));
        var scanned = scan.Branches.Select(branch => branch.Facts.Reference.FullName).ToHashSet();
        var listed = Assert.Single(progress.Of<ScanReferencesListed>());
        Assert.Equal(
            scanned,
            listed.References.Select(reference => reference.ReferenceName).ToHashSet());
        Assert.Equal(
            scanned,
            progress.Of<ScanReferenceMeasured>().Select(item => item.ReferenceName).ToHashSet());
        Assert.Equal(
            scanned,
            progress.Of<ScanReferenceEnriched>().Select(item => item.ReferenceName).ToHashSet());
    }

    [Fact]
    public async Task ReferencesAreAnnouncedBeforeTheyAreRead()
    {
        using var repository = GitTestRepository.Create();
        var progress = new RecordingScanProgress();
        var scanner = GitScannerFactory.Create();

        await scanner.ScanAsync(Request(repository), progress, default);

        var events = progress.Events;
        var listed = events.ToList().FindIndex(item => item is ScanReferencesListed);
        var firstMeasured = events.ToList().FindIndex(item => item is ScanReferenceMeasured);
        var enrichmentStarted = events.ToList().FindIndex(
            item => item is ScanStageStarted { Stage: RepositoryScanStage.Enrichment });
        Assert.InRange(listed, 0, firstMeasured - 1);
        Assert.InRange(firstMeasured, 0, enrichmentStarted - 1);
    }

    [Fact]
    public async Task MeasuredEventCarriesTheDistanceAndTheSharedCommit()
    {
        using var repository = GitTestRepository.Create();
        var progress = new RecordingScanProgress();
        var scanner = GitScannerFactory.Create();

        var result = await scanner.ScanAsync(Request(repository), progress, default);

        Assert.True(result.TryGetValue(out var scan));
        foreach (var measured in progress.Of<ScanReferenceMeasured>())
        {
            var branch = scan.Branches.Single(
                item => item.Facts.Reference.FullName == measured.ReferenceName);
            Assert.Equal(branch.Facts.Divergence, measured.Divergence);
        }

        var diverged = progress
            .Of<ScanReferenceMeasured>()
            .Single(item => item.ReferenceName == "refs/heads/feature/diverged");
        Assert.Equal(repository.ResolveCommit("refs/heads/main~1"), diverged.MergeBaseCommit);
    }

    [Fact]
    public async Task UnrelatedHistoryIsReportedWithoutASharedCommit()
    {
        using var repository = GitTestRepository.Create();
        var progress = new RecordingScanProgress();
        var scanner = GitScannerFactory.Create();

        await scanner.ScanAsync(Request(repository), progress, default);

        var orphan = progress
            .Of<ScanReferenceMeasured>()
            .Single(item => item.ReferenceName == "refs/heads/feature/orpheline");
        Assert.Null(orphan.MergeBaseCommit);
        Assert.Equal(BranchRelationship.NoCommonAncestor, orphan.Divergence.Relationship);
    }

    [Fact]
    public async Task EveryGitCommandIsReportedAsItWouldBeTyped()
    {
        using var repository = GitTestRepository.Create();
        var before = repository.TakeSnapshot();
        var progress = new RecordingScanProgress();
        var scanner = GitScannerFactory.Create();

        await scanner.ScanAsync(Request(repository), progress, default);

        var commands = progress.Of<ScanCommandCompleted>().ToArray();
        Assert.Contains(commands, command => command.CommandLine == "git --version");
        Assert.Contains(
            commands,
            command => command.CommandLine.StartsWith("git for-each-ref", StringComparison.Ordinal));
        Assert.All(commands, command => Assert.StartsWith("git ", command.CommandLine));
        Assert.DoesNotContain(
            commands,
            command => command.CommandLine.Contains("-C ", StringComparison.Ordinal));
        Assert.Equal(before, repository.TakeSnapshot());
    }

    /// <summary>An unfollowed scan reads the same repository, without the narration.</summary>
    [Fact]
    public async Task UnfollowedScanReadsTheSameRepository()
    {
        using var repository = GitTestRepository.Create();
        var progress = new RecordingScanProgress();
        var scanner = GitScannerFactory.Create();

        var followed = await scanner.ScanAsync(Request(repository), progress, default);
        var silent = await scanner.ScanAsync(Request(repository), default);

        Assert.True(followed.TryGetValue(out var followedScan));
        Assert.True(silent.TryGetValue(out var silentScan));
        Assert.Equal(
            followedScan.Branches.Select(branch => branch.Facts),
            silentScan.Branches.Select(branch => branch.Facts));
    }

    private static RepositoryScanRequest Request(GitTestRepository repository) =>
        new(repository.RepositoryPath, new GitRef(Baseline));
}
