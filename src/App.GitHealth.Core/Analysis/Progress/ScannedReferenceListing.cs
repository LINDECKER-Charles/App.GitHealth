namespace App.GitHealth.Core.Analysis;

/// <summary>
/// What one reference already tells about itself the moment it is listed, before any
/// comparison against the baseline: its tip, and who moved it last.
/// </summary>
public sealed record ScannedReferenceListing
{
    public required string ReferenceName { get; init; }

    public required string CommitId { get; init; }

    public DateTimeOffset? LastActivityAtUtc { get; init; }

    public string? TipAuthor { get; init; }
}
