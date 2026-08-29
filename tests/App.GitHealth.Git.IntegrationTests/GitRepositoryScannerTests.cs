using App.GitHealth.Core.Analysis;
using App.GitHealth.Core.Branches;
using App.GitHealth.Git.IntegrationTests.Fixtures;

namespace App.GitHealth.Git.IntegrationTests;

public sealed class GitRepositoryScannerTests
{
    [Fact]
    public async Task InspectDetectsStandardBareAndLinkedWorktreeRepositories()
    {
        using var repository = GitTestRepository.Create();
        var scanner = GitScannerFactory.Create();

        var standard = await scanner.InspectAsync(repository.RepositoryPath, default);
        var alias = await scanner.InspectAsync(repository.CreateRepositoryLink(), default);
        var bare = await scanner.InspectAsync(repository.CreateBareClone(), default);
        var worktree = await scanner.InspectAsync(repository.CreateLinkedWorktree(), default);

        Assert.True(standard.TryGetValue(out var standardValue));
        Assert.Equal("refs/remotes/origin/main", standardValue.SuggestedReference?.FullName);
        Assert.DoesNotContain(
            standardValue.References,
            reference => reference.FullName == "refs/remotes/origin/HEAD");
        Assert.True(alias.TryGetValue(out var aliasValue));
        Assert.Equal(standardValue.Location.CanonicalPath, aliasValue.Location.CanonicalPath);
        Assert.True(bare.TryGetValue(out var bareValue));
        Assert.True(bareValue.Location.IsBare);
        Assert.True(worktree.TryGetValue(out var worktreeValue));
        Assert.NotNull(worktreeValue.Location.WorkingTreePath);
    }

    [Fact]
    public async Task ScanCalculatesTopologyContributorsAndPreservesRepository()
    {
        using var repository = GitTestRepository.Create();
        var before = repository.TakeSnapshot();
        var scanner = GitScannerFactory.Create();
        var request = new RepositoryScanRequest(
            repository.RepositoryPath,
            new GitRef("refs/heads/main"));

        var result = await scanner.ScanAsync(request, default);

        Assert.True(result.TryGetValue(out var scan));
        AssertExpectedTopologies(scan);

        var ahead = FindBranch(scan, "refs/heads/feature/ahead");
        var contributor = Assert.Single(ahead.Contributors);
        Assert.Equal("Ada Lovelace", contributor.Name);
        Assert.Equal(2, contributor.CommitCount);
        Assert.NotNull(ahead.Facts.Tip.LastActivityAt);
        Assert.Equal("Ada Lovelace", ahead.Facts.Tip.Author);
        Assert.Equal(before, repository.TakeSnapshot());
    }

    [Fact]
    public async Task ContainsCommitDistinguishesPresentAndAbsentObjects()
    {
        using var repository = GitTestRepository.Create();
        var before = repository.TakeSnapshot();
        var scanner = GitScannerFactory.Create(repositoriesRoot: repository.RepositoryPath);
        var presentCommit = new CommitId(repository.ResolveCommit("refs/heads/main"));
        var absentCommit = new CommitId(new string('0', presentCommit.Value.Length));

        var present = await scanner.ContainsCommitAsync(
            repository.RepositoryPath,
            presentCommit,
            default);
        var absent = await scanner.ContainsCommitAsync(
            repository.RepositoryPath,
            absentCommit,
            default);

        Assert.True(present.TryGetValue(out var isPresent));
        Assert.True(isPresent);
        Assert.True(absent.TryGetValue(out var isAbsent));
        Assert.False(isAbsent);
        Assert.Equal(before, repository.TakeSnapshot());
    }

    private static void AssertExpectedTopologies(RepositoryScan scan)
    {
        AssertBranch(
            scan,
            "refs/heads/feature/ahead",
            BranchDivergence.Create(2, 0, BranchRelationship.CommonAncestor));
        AssertBranch(
            scan,
            "refs/heads/feature/merged",
            BranchDivergence.Create(
                0,
                2,
                BranchRelationship.BranchIsAncestorOfReference));
        AssertBranch(
            scan,
            "refs/heads/feature/diverged",
            BranchDivergence.Create(1, 1, BranchRelationship.CommonAncestor));
        AssertBranch(
            scan,
            "refs/heads/feature/orpheline",
            BranchDivergence.Create(1, 4, BranchRelationship.NoCommonAncestor));
        AssertBranch(
            scan,
            "refs/heads/feature/merge-reference",
            BranchDivergence.Create(2, 0, BranchRelationship.CommonAncestor));
        Assert.Contains(scan.Branches, branch => branch.Facts.Reference.FullName.Contains('é'));
    }

    [Fact]
    public async Task FallbackProducesTheSameFactsAsAheadBehind()
    {
        using var repository = GitTestRepository.Create();
        var request = new RepositoryScanRequest(
            repository.RepositoryPath,
            new GitRef("refs/heads/main"));

        var fast = await GitScannerFactory.Create().ScanAsync(request, default);
        var fallback = await GitScannerFactory.Create(useAheadBehind: false)
            .ScanAsync(request, default);

        Assert.True(fast.TryGetValue(out var fastScan));
        Assert.True(fallback.TryGetValue(out var fallbackScan));
        var fastFacts = fastScan.Branches.ToDictionary(
            branch => branch.Facts.Reference.FullName,
            branch => branch.Facts.Divergence);
        var fallbackFacts = fallbackScan.Branches.ToDictionary(
            branch => branch.Facts.Reference.FullName,
            branch => branch.Facts.Divergence);
        Assert.Equal(fastFacts, fallbackFacts);
    }

    [Fact]
    public async Task InvalidPathReturnsAFunctionalError()
    {
        var scanner = GitScannerFactory.Create();

        var missingPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());

        var result = await scanner.InspectAsync(missingPath, default);

        Assert.False(result.IsSuccess);
        Assert.Equal(RepositoryErrorCode.PathNotFound, result.Error?.Code);
    }

    [Fact]
    public async Task InspectRejectsGitMetadataOutsideTheAllowedRoot()
    {
        using var repository = GitTestRepository.Create();
        repository.MoveMetadataOutsideRepository();
        var scanner = GitScannerFactory.Create(repositoriesRoot: repository.RepositoryPath);

        var result = await scanner.InspectAsync(repository.RepositoryPath, default);

        Assert.False(result.IsSuccess);
        Assert.Equal(RepositoryErrorCode.PathNotAllowed, result.Error?.Code);
    }

    [Fact]
    public async Task InspectRejectsCommonDirectoryOutsideTheAllowedRoot()
    {
        using var repository = GitTestRepository.Create();
        var scenario = repository.CreateExternalCommonDirectoryScenario();
        var scanner = GitScannerFactory.Create(repositoriesRoot: scenario.RootPath);

        var result = await scanner.InspectAsync(scenario.WorktreePath, default);

        Assert.False(result.IsSuccess);
        Assert.Equal(RepositoryErrorCode.PathNotAllowed, result.Error?.Code);
    }

    [Fact]
    public async Task InspectRejectsNestedAlternateOutsideTheAllowedRoot()
    {
        using var repository = GitTestRepository.Create();
        repository.ConfigureNestedExternalAlternate();
        var scanner = GitScannerFactory.Create(repositoriesRoot: repository.RepositoryPath);

        var result = await scanner.InspectAsync(repository.RepositoryPath, default);

        Assert.False(result.IsSuccess);
        Assert.Equal(RepositoryErrorCode.PathNotAllowed, result.Error?.Code);
    }

    [Fact]
    public async Task InspectAcceptsNestedAlternatesInsideTheAllowedRoot()
    {
        using var repository = GitTestRepository.Create();
        repository.ConfigureNestedAllowedAlternates();
        var scanner = GitScannerFactory.Create(repositoriesRoot: repository.RepositoryPath);

        var result = await scanner.InspectAsync(repository.RepositoryPath, default);

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task CancellationIsPropagatedInsteadOfBecomingAFunctionalError()
    {
        using var repository = GitTestRepository.Create();
        var scanner = GitScannerFactory.Create();
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            scanner.InspectAsync(repository.RepositoryPath, cancellation.Token));
    }

    private static void AssertBranch(
        RepositoryScan scan,
        string fullName,
        BranchDivergence expected)
    {
        var branch = FindBranch(scan, fullName);
        Assert.Equal(expected, branch.Facts.Divergence);
    }

    private static ScannedBranch FindBranch(RepositoryScan scan, string fullName)
    {
        return Assert.Single(
            scan.Branches,
            branch => branch.Facts.Reference.FullName == fullName);
    }
}
