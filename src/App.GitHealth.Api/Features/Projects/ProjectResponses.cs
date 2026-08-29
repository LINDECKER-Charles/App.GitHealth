namespace App.GitHealth.Api.Features.Projects;

internal sealed record RepositoryValidationResponse
{
    public required string CanonicalPath { get; init; }

    public required bool IsBare { get; init; }

    public string? SuggestedReference { get; init; }

    public required IReadOnlyList<string> References { get; init; }
}

internal sealed record ProjectResponse
{
    public required Guid Id { get; init; }

    public required string DisplayName { get; init; }

    public required string RepositoryPath { get; init; }

    public required bool IsRepositoryAccessible { get; init; }

    public required DateTimeOffset CreatedAtUtc { get; init; }

    public required DateTimeOffset UpdatedAtUtc { get; init; }

    public string? ReferenceName { get; init; }

    public required string BranchNamespace { get; init; }

    public required int ActiveUntilDays { get; init; }

    public required int InactiveAfterDays { get; init; }

    public required IReadOnlyList<string> ExcludedPatterns { get; init; }

    public required IReadOnlyList<string> ProtectedPatterns { get; init; }

    public required bool IsFavorite { get; init; }

    public string? GroupName { get; init; }

    public Guid? LastSuccessfulAnalysisId { get; init; }
}
