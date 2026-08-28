namespace App.GitHealth.Api.Git;

public sealed class GitScannerOptions
{
    public TimeSpan CommandTimeout { get; init; } = TimeSpan.FromSeconds(30);

    public int MaximumOutputCharacters { get; init; } = 4 * 1024 * 1024;

    public int MaximumParallelCommands { get; init; } = 4;

    public bool UseAheadBehind { get; init; } = true;
}
