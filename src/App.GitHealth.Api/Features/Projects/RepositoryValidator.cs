using App.GitHealth.Api.Features.Common;
using App.GitHealth.Core.Analysis;
using Microsoft.Extensions.Options;

namespace App.GitHealth.Api.Features.Projects;

internal sealed class RepositoryValidator(
    IRepositoryScanner scanner,
    IOptions<RepositoryAccessOptions> options)
{
    public async Task<ApiOutcome<RepositoryDescriptor>> ValidateAsync(
        string? repositoryPath,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(repositoryPath))
        {
            return ApiOutcome<RepositoryDescriptor>.Failed(ApiProblems.BadRequest(
                ApiErrorCodes.InvalidPath,
                "Le chemin du dépôt est obligatoire."));
        }

        var inspected = await scanner.InspectAsync(repositoryPath, cancellationToken);
        if (!inspected.TryGetValue(out var descriptor))
        {
            return ApiOutcome<RepositoryDescriptor>.Failed(
                ApiProblems.FromRepository(inspected.Error!));
        }

        return IsAllowed(descriptor.Location.CanonicalPath)
            ? ApiOutcome<RepositoryDescriptor>.Success(descriptor)
            : ApiOutcome<RepositoryDescriptor>.Failed(ApiProblems.BadRequest(
                ApiErrorCodes.PathNotAllowed,
                "Le dépôt se trouve hors de la racine autorisée."));
    }

    private bool IsAllowed(string canonicalRepositoryPath)
    {
        var configuredRoot = options.Value.RepositoriesRoot;
        if (string.IsNullOrWhiteSpace(configuredRoot))
        {
            return true;
        }

        var root = ResolvePhysicalPath(configuredRoot);
        var repository = ResolvePhysicalPath(canonicalRepositoryPath);
        var relative = Path.GetRelativePath(root, repository);
        return relative != ".."
            && !relative.StartsWith($"..{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
            && !Path.IsPathRooted(relative);
    }

    private static string ResolvePhysicalPath(string path)
    {
        var directory = new DirectoryInfo(Path.GetFullPath(path));
        if (!directory.Exists)
        {
            return directory.FullName;
        }

        return ResolveDirectory(directory);
    }

    private static string ResolveDirectory(DirectoryInfo directory)
    {
        if (directory.Parent is null)
        {
            return directory.FullName;
        }

        var parent = ResolveDirectory(directory.Parent);
        var candidate = new DirectoryInfo(Path.Combine(parent, directory.Name));
        return candidate.ResolveLinkTarget(returnFinalTarget: true)?.FullName
            ?? candidate.FullName;
    }
}
