namespace App.GitHealth.Api.Git;

public sealed class GitScannerOptions
{
    public const int MinimumCommandTimeoutSeconds = 1;
    public const int MaximumCommandTimeoutSeconds = 120;
    public const int MinimumOutputBytes = 1024;
    public const int MaximumOutputBytesLimit = 16 * 1024 * 1024;
    public const int MinimumParallelCommands = 1;
    public const int MaximumParallelCommandsLimit = 8;

    public TimeSpan CommandTimeout { get; init; } = TimeSpan.FromSeconds(30);

    public int MaximumOutputBytes { get; init; } = 4 * 1024 * 1024;

    public int MaximumParallelCommands { get; init; } = 4;

    public bool UseAheadBehind { get; init; } = true;

    /// <summary>
    /// Explicit path of the Git executable. When empty, resolution falls back to the
    /// <c>PATH</c>, then to the standard installation locations.
    /// </summary>
    public string? ExecutablePath { get; init; }

    internal string? RepositoriesRoot { get; set; }
}
