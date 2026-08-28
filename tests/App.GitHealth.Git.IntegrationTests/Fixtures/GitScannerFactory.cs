using App.GitHealth.Api.Git;
using App.GitHealth.Api.Git.Process;
using Microsoft.Extensions.Options;

namespace App.GitHealth.Git.IntegrationTests.Fixtures;

internal static class GitScannerFactory
{
    private static readonly DateTimeOffset ScanTime =
        new(2026, 8, 28, 12, 0, 0, TimeSpan.Zero);

    public static GitRepositoryScanner Create(bool useAheadBehind = true)
    {
        var settings = Options.Create(new GitScannerOptions
        {
            UseAheadBehind = useAheadBehind,
        });
        var runner = new GitProcessRunner(settings);
        return new GitRepositoryScanner(runner, settings, new FixedClock(ScanTime));
    }
}
