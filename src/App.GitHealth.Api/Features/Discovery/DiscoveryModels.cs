namespace App.GitHealth.Api.Features.Discovery;

internal sealed record RepositoryDiscoveryRequest
{
    public string? Path { get; init; }

    public int? Depth { get; init; }
}

/// <summary>Raw result of the file walk, before any Git read.</summary>
internal sealed record RepositorySearch(IReadOnlyList<string> Paths, bool IsTruncated);

internal sealed record DiscoveredRepositoryResponse
{
    public required string CanonicalPath { get; init; }

    public required string SuggestedName { get; init; }

    public string? SuggestedReference { get; init; }

    public required int ReferenceCount { get; init; }

    public required bool IsBare { get; init; }

    /// <summary>Project already registered for this repository, or <c>null</c> if it is
    /// still to be added.</summary>
    public Guid? TrackedProjectId { get; init; }
}

internal sealed record RepositoryDiscoveryResponse
{
    public required string RootPath { get; init; }

    public required IReadOnlyList<DiscoveredRepositoryResponse> Repositories { get; init; }

    public required bool IsTruncated { get; init; }
}
