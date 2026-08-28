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

        var accessFailure = CheckAccess(repositoryPath);
        if (accessFailure is not null)
        {
            return ApiOutcome<RepositoryDescriptor>.Failed(accessFailure);
        }

        var inspected = await scanner.InspectAsync(repositoryPath, cancellationToken);
        if (!inspected.TryGetValue(out var descriptor))
        {
            return ApiOutcome<RepositoryDescriptor>.Failed(
                ApiProblems.FromRepository(inspected.Error!));
        }

        accessFailure = CheckAccess(descriptor.Location.CanonicalPath);
        return accessFailure is null
            ? ApiOutcome<RepositoryDescriptor>.Success(descriptor)
            : ApiOutcome<RepositoryDescriptor>.Failed(accessFailure);
    }

    private ApiFailure? CheckAccess(string path)
    {
        try
        {
            return IsAllowed(path) ? null : ApiProblems.BadRequest(
                ApiErrorCodes.PathNotAllowed,
                "Le dépôt se trouve hors de la racine autorisée.");
        }
        catch (Exception exception) when (exception is ArgumentException
            or IOException or UnauthorizedAccessException)
        {
            return ApiProblems.BadRequest(ApiErrorCodes.InvalidPath, exception.Message);
        }
    }

    private bool IsAllowed(string canonicalRepositoryPath)
    {
        var configuredRoot = options.Value.RepositoriesRoot;
        if (string.IsNullOrWhiteSpace(configuredRoot))
        {
            return true;
        }

        var root = Path.GetFullPath(configuredRoot);
        var repository = Path.GetFullPath(canonicalRepositoryPath);
        var physicalRoot = ResolveLink(root);
        var relative = RelativeToKnownRoot(root, physicalRoot, repository);
        if (relative is null)
        {
            return false;
        }

        var physicalRepository = ResolveFromRoot(physicalRoot, relative);
        return !LeavesRoot(Path.GetRelativePath(physicalRoot, physicalRepository));
    }

    private static string? RelativeToKnownRoot(
        string lexicalRoot,
        string physicalRoot,
        string repository)
    {
        var lexicalRelative = Path.GetRelativePath(lexicalRoot, repository);
        if (!LeavesRoot(lexicalRelative))
        {
            return lexicalRelative;
        }

        var physicalRelative = Path.GetRelativePath(physicalRoot, repository);
        return LeavesRoot(physicalRelative) ? null : physicalRelative;
    }

    private static string ResolveFromRoot(string physicalRoot, string relativePath)
    {
        var current = physicalRoot;
        foreach (var segment in relativePath.Split(
            [Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar],
            StringSplitOptions.RemoveEmptyEntries))
        {
            current = ResolveLink(Path.Combine(current, segment));
        }

        return current;
    }

    private static string ResolveLink(string path)
    {
        var directory = new DirectoryInfo(path);
        if (!directory.Exists)
        {
            return directory.FullName;
        }

        return directory.ResolveLinkTarget(returnFinalTarget: true)?.FullName
            ?? directory.FullName;
    }

    private static bool LeavesRoot(string relativePath) => relativePath == ".."
        || relativePath.StartsWith($"..{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
        || Path.IsPathRooted(relativePath);
}
