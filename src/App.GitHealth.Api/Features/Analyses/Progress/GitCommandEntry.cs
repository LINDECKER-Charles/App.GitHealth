namespace App.GitHealth.Api.Features.Analyses;

/// <summary>One Git command a running analysis has already finished, ready to be read.</summary>
internal sealed record GitCommandEntry
{
    /// <summary>Rank of the command in the run: what lets a reader append without repeating.</summary>
    public required int Sequence { get; init; }

    public required string CommandLine { get; init; }

    public required int DurationMs { get; init; }

    public required int ExitCode { get; init; }

    public string? Output { get; init; }
}
