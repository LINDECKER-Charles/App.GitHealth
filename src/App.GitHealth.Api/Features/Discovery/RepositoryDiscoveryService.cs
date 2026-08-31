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
/// Detects the repositories under a folder, reads them read only and flags the ones already
/// registered.
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
                "The folder path is missing or too long."));
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
                "The requested folder is not accessible."));
        }
        catch (Exception exception) when (exception is ArgumentException
            or IOException or NotSupportedException)
        {
            return Failed(ApiProblems.BadRequest(
                ApiErrorCodes.InvalidDirectory,
                "The folder path is invalid."));
        }
    }

    private ApiOutcome<string> ResolveExistingRoot(string fullPath)
    {
        if (!RepositoryPathGuard.IsAllowed(options.Value.RepositoriesRoot, fullPath))
        {
            return Failed(ApiProblems.Forbidden(
                ApiErrorCodes.DirectoryNotAllowed,
                "The requested folder is outside the allowed root."));
        }

        return Directory.Exists(fullPath)
            ? ApiOutcome<string>.Success(fullPath)
            : Failed(ApiProblems.NotFound(
                ApiErrorCodes.DirectoryNotFound,
                "The requested folder does not exist."));
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
    /// Every candidate is confirmed by a Git read: a folder that looks like a repository
    /// without being a readable one is simply dropped from the result.
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
