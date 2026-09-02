namespace App.GitHealth.Api.Features.Baselines;

internal sealed record BaselineUpdateRequest
{
    /// <summary>The whole ordered list. The first entry becomes the primary baseline.</summary>
    public string[] ReferenceNames { get; init; } = [];
}

internal sealed record BaselineResponse
{
    public required string ReferenceName { get; init; }

    public required int Position { get; init; }

    public required bool IsPrimary { get; init; }

    public Guid? LastSuccessfulAnalysisId { get; init; }

    public DateTimeOffset? LastCapturedAtUtc { get; init; }

    public required int BranchCount { get; init; }
}

internal sealed record BaselineListResponse
{
    public required IReadOnlyList<BaselineResponse> Items { get; init; }

    /// <summary>
    /// References the repository offers right now. Empty when it cannot be read: the caller
    /// then shows what is configured rather than nothing at all.
    /// </summary>
    public required IReadOnlyList<string> AvailableReferences { get; init; }
}
