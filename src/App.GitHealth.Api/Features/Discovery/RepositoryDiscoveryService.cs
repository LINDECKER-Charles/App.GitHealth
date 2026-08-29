using System.Collections.Concurrent;
using System.Security;
using App.GitHealth.Api.Features.Common;
using App.GitHealth.Api.Features.Projects;
using App.GitHealth.Api.Git.Paths;
using App.GitHealth.Api.Persistence.Repositories;
using App.GitHealth.Core.Analysis;
using Microsoft.Extensions.Options;

namespace App.GitHealth.Api.Features.Discovery;

/// <summary>
/// Détecte les dépôts d'un dossier, les lit en lecture seule et signale ceux déjà enregistrés.
/// </summary>
internal sealed class RepositoryDiscoveryService(
    RepositoryValidator validator,
    IProjectRepository projects,
    IOptions<RepositoryAccessOptions> options)
{
    private const int MaximumParallelInspections = 4;

    public async Task<ApiOutcome<RepositoryDiscoveryResponse>> DiscoverAsync(
        RepositoryDiscoveryRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var root = ResolveRoot(request.Path);
        if (!root.IsSuccess)
        {
            return ApiOutcome<RepositoryDiscoveryResponse>.Failed(root.Failure!);
        }

        var search = RepositoryFinder.Find(
            root.Value!,
            RepositoryFinder.ClampDepth(request.Depth),
            options.Value.RepositoriesRoot);
        var tracked = await ReadTrackedPathsAsync(cancellationToken);
        var repositories = await InspectAsync(search.Paths, tracked, cancellationToken);
        return ApiOutcome<RepositoryDiscoveryResponse>.Success(new RepositoryDiscoveryResponse
        {
            RootPath = root.Value!,
            Repositories = repositories,
            IsTruncated = search.IsTruncated,
        });
    }

    private ApiOutcome<string> ResolveRoot(string? requestedPath)
    {
        if (string.IsNullOrWhiteSpace(requestedPath)
            || requestedPath.Length > RepositoryValidator.MaximumPathLength)
        {
            return Failed(ApiProblems.BadRequest(
                ApiErrorCodes.InvalidDirectory,
                "Le chemin du dossier est absent ou trop long."));
        }

        try
        {
            return ResolveExistingRoot(Path.GetFullPath(requestedPath));
        }
        catch (Exception exception) when (exception is UnauthorizedAccessException
            or SecurityException)
        {
            return Failed(ApiProblems.Forbidden(
                ApiErrorCodes.DirectoryInaccessible,
                "Le dossier demandé n’est pas accessible."));
        }
        catch (Exception exception) when (exception is ArgumentException
            or IOException or NotSupportedException)
        {
            return Failed(ApiProblems.BadRequest(
                ApiErrorCodes.InvalidDirectory,
                "Le chemin du dossier est invalide."));
        }
    }

    private ApiOutcome<string> ResolveExistingRoot(string fullPath)
    {
        if (!RepositoryPathGuard.IsAllowed(options.Value.RepositoriesRoot, fullPath))
        {
            return Failed(ApiProblems.Forbidden(
                ApiErrorCodes.DirectoryNotAllowed,
                "Le dossier demandé se trouve hors de la racine autorisée."));
        }

        return Directory.Exists(fullPath)
            ? ApiOutcome<string>.Success(fullPath)
            : Failed(ApiProblems.NotFound(
                ApiErrorCodes.DirectoryNotFound,
                "Le dossier demandé n’existe pas."));
    }

    private async Task<IReadOnlyDictionary<string, Guid>> ReadTrackedPathsAsync(
        CancellationToken cancellationToken)
    {
        var stored = await projects.ListAsync(cancellationToken);
        var tracked = new Dictionary<string, Guid>(StringComparer.Ordinal);
        foreach (var project in stored)
        {
            tracked[project.RepositoryPath] = project.Id;
        }

        return tracked;
    }

    /// <summary>
    /// Chaque candidat est confirmé par une lecture Git : un dossier qui ressemble à un dépôt
    /// sans en être un lisible est simplement écarté du résultat.
    /// </summary>
    private async Task<IReadOnlyList<DiscoveredRepositoryResponse>> InspectAsync(
        IReadOnlyList<string> paths,
        IReadOnlyDictionary<string, Guid> tracked,
        CancellationToken cancellationToken)
    {
        var found = new ConcurrentBag<DiscoveredRepositoryResponse>();
        var parallelOptions = new ParallelOptions
        {
            CancellationToken = cancellationToken,
            MaxDegreeOfParallelism = MaximumParallelInspections,
        };
        await Parallel.ForEachAsync(paths, parallelOptions, async (path, token) =>
        {
            var validation = await validator.ValidateAsync(path, token);
            if (validation.IsSuccess)
            {
                found.Add(Describe(validation.Value!, tracked));
            }
        });
        return found
            .OrderBy(repository => repository.CanonicalPath, StringComparer.Ordinal)
            .ToArray();
    }

    private static DiscoveredRepositoryResponse Describe(
        RepositoryDescriptor descriptor,
        IReadOnlyDictionary<string, Guid> tracked)
    {
        var canonicalPath = descriptor.Location.CanonicalPath;
        var references = descriptor.References;
        var reference = descriptor.SuggestedReference
            ?? (references.Count > 0 ? references[0] : null);
        return new DiscoveredRepositoryResponse
        {
            CanonicalPath = canonicalPath,
            SuggestedName = new DirectoryInfo(canonicalPath).Name,
            SuggestedReference = reference?.FullName,
            ReferenceCount = references.Count,
            IsBare = descriptor.Location.IsBare,
            TrackedProjectId = tracked.TryGetValue(canonicalPath, out var id) ? id : null,
        };
    }

    private static ApiOutcome<string> Failed(ApiFailure failure) =>
        ApiOutcome<string>.Failed(failure);
}
