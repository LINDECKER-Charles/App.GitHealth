namespace App.GitHealth.Core.Analysis;

/// <summary>
/// A reference has given up its authors: the scan has nothing left to read about it.
/// </summary>
public sealed record ScanReferenceEnriched : RepositoryScanEvent
{
    public required string ReferenceName { get; init; }

    /// <summary>Author of most of the commits the reference adds; null when it adds none.</summary>
    public string? TopContributor { get; init; }

    public required int ContributorCount { get; init; }
}
