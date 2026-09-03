namespace App.GitHealth.Core.Analysis;

/// <summary>
/// One Git command the scan has just run. Reported so a reader can watch, line by line,
/// exactly what GitHealth asked of the repository — every one of them a read.
/// </summary>
public sealed record ScanCommandCompleted : RepositoryScanEvent
{
    /// <summary>Command as it would be typed, without the hardening flags GitHealth adds.</summary>
    public required string CommandLine { get; init; }

    public required TimeSpan Duration { get; init; }

    public required int ExitCode { get; init; }

    /// <summary>First line of what Git answered, shortened; null when it answered nothing.</summary>
    public string? Output { get; init; }
}
