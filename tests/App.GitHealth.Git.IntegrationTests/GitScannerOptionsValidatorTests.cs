using App.GitHealth.Api.Git;

namespace App.GitHealth.Git.IntegrationTests;

public sealed class GitScannerOptionsValidatorTests
{
    [Fact]
    public void ValidBoundsAreAccepted()
    {
        var options = new GitScannerOptions
        {
            CommandTimeout = TimeSpan.FromSeconds(120),
            MaximumOutputBytes = GitScannerOptions.MaximumOutputBytesLimit,
            MaximumParallelCommands = 8,
        };

        Assert.True(new GitScannerOptionsValidator().Validate(null, options).Succeeded);
    }

    [Theory]
    [InlineData(0, 4096, 1)]
    [InlineData(121, 4096, 1)]
    [InlineData(30, 1023, 1)]
    [InlineData(30, 16777217, 1)]
    [InlineData(30, 4096, 0)]
    [InlineData(30, 4096, 9)]
    public void UnsafeBoundsAreRejected(int seconds, int bytes, int concurrency)
    {
        var options = new GitScannerOptions
        {
            CommandTimeout = TimeSpan.FromSeconds(seconds),
            MaximumOutputBytes = bytes,
            MaximumParallelCommands = concurrency,
        };

        Assert.False(new GitScannerOptionsValidator().Validate(null, options).Succeeded);
    }
}
