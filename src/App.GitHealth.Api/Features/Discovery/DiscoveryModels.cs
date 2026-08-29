namespace App.GitHealth.Api.Features.Discovery;

internal sealed record RepositoryDiscoveryRequest
{
    public string? Path { get; init; }

    public int? Depth { get; init; }
}

/// <summary>Résultat brut du parcours de fichiers, avant toute lecture Git.</summary>
internal sealed record RepositorySearch(IReadOnlyList<string> Paths, bool IsTruncated);

internal sealed record DiscoveredRepositoryResponse
{
    public required string CanonicalPath { get; init; }

    public required string SuggestedName { get; init; }

    public string? SuggestedReference { get; init; }

    public required int ReferenceCount { get; init; }

    public required bool IsBare { get; init; }

    /// <summary>Projet déjà enregistré sur ce dépôt, ou <c>null</c> s'il reste à ajouter.</summary>
    public Guid? TrackedProjectId { get; init; }
}

internal sealed record RepositoryDiscoveryResponse
{
    public required string RootPath { get; init; }

    public required IReadOnlyList<DiscoveredRepositoryResponse> Repositories { get; init; }

    public required bool IsTruncated { get; init; }
}
