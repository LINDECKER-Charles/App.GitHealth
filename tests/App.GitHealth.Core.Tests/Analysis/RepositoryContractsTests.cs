using App.GitHealth.Core.Analysis;
using App.GitHealth.Core.Branches;

namespace App.GitHealth.Core.Tests.Analysis;

public sealed class RepositoryContractsTests
{
    [Fact]
    public void ScanRequestRejectsMissingValues()
    {
        var reference = new GitRef("refs/heads/main");

        Assert.Throws<ArgumentException>(() => new RepositoryScanRequest("", reference));
        Assert.Throws<ArgumentException>(() => new RepositoryScanRequest("repo", reference, ""));
    }

    [Fact]
    public void ScanMetadataRequiresUtcAndGitVersion()
    {
        var localTime = new DateTimeOffset(2026, 8, 28, 12, 0, 0, TimeSpan.FromHours(2));

        Assert.Throws<ArgumentException>(() => new RepositoryScanMetadata(localTime, "git 2.55"));
        Assert.Throws<ArgumentException>(() =>
            new RepositoryScanMetadata(DateTimeOffset.UnixEpoch, ""));
    }

    [Fact]
    public void ResultExposesEitherAValueOrAFunctionalError()
    {
        var success = RepositoryResults.Success("repository");
        var failure = RepositoryResults.Failure<string>(
            new RepositoryError(RepositoryErrorCode.NotARepository, "Dépôt invalide"));

        Assert.True(success.TryGetValue(out var value));
        Assert.Equal("repository", value);
        Assert.False(failure.TryGetValue(out _));
        Assert.Equal(RepositoryErrorCode.NotARepository, failure.Error?.Code);
    }
}
