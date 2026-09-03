namespace App.GitHealth.Api.Features.Analyses;

/// <summary>
/// A running analysis frozen at one instant: the phase it is in, the ledger of its
/// references and the tail of the Git commands it has run.
/// </summary>
internal sealed record AnalysisProgressSnapshot
{
    public required AnalysisPhase Phase { get; init; }

    public required DateTimeOffset UpdatedAtUtc { get; init; }

    public string? Message { get; init; }

    public required IReadOnlyList<ReferenceProgress> References { get; init; }

    /// <summary>Most recent commands only: the run keeps a tail, not the whole history.</summary>
    public required IReadOnlyList<GitCommandEntry> Commands { get; init; }

    public required int CommandCount { get; init; }
}
