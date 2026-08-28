namespace App.GitHealth.Api.Features.Policies;

internal sealed record PolicyUpdateRequest
{
    public int ActiveUntilDays { get; init; } = 30;

    public int InactiveAfterDays { get; init; } = 90;

    public string[] ExcludedPatterns { get; init; } = [];

    public string[] ProtectedPatterns { get; init; } = [];
}

internal sealed record PolicyPreviewResponse
{
    public required IReadOnlyList<PolicyPreviewMatchResponse> Matches { get; init; }
}

internal sealed record PolicyPreviewMatchResponse
{
    public required string ReferenceName { get; init; }

    public required bool IsExcluded { get; init; }

    public required bool IsProtected { get; init; }

    public required string Reason { get; init; }
}
