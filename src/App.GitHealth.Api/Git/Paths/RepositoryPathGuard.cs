using App.GitHealth.Api.Git.Models;
using App.GitHealth.Api.Git.Process;
using App.GitHealth.Core.Analysis;

namespace App.GitHealth.Api.Git.Paths;

internal static class RepositoryPathGuard
{
    public static bool IsAllowed(string? repositoriesRoot, string path)
    {
        if (string.IsNullOrWhiteSpace(repositoriesRoot))
        {
            return true;
        }

        var physicalRoot = ResolvePhysicalPath(repositoriesRoot);
        var physicalPath = ResolvePhysicalPath(path);
        return IsWithin(physicalRoot, physicalPath);
    }

    public static void EnsureAllowed(
        string? repositoriesRoot,
        GitRepositoryContext context)
    {
        var paths = new[]
        {
            context.CanonicalPath,
            context.GitDirectory,
            context.CommonDirectory,
            context.ObjectDirectory,
            context.WorkingTreePath,
        };
        if (paths.Where(path => path is not null)
            .All(path => IsAllowed(repositoriesRoot, path!)))
        {
            return;
        }

        throw new GitProcessException(
            RepositoryErrorCode.PathNotAllowed,
            "The repository or its Git metadata is outside the allowed root.");
    }

    public static string ResolvePhysicalPath(string path)
    {
        var fullPath = Path.GetFullPath(path);
        var root = Path.GetPathRoot(fullPath)
            ?? throw new ArgumentException("The path must be absolute.", nameof(path));
        var current = root;
        var relative = fullPath[root.Length..];
        foreach (var segment in Split(relative))
        {
            current = ResolveLink(Path.Combine(current, segment));
        }

        return Path.GetFullPath(current);
    }

    private static string[] Split(string relativePath) =>
        relativePath.Split(
            [Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar],
            StringSplitOptions.RemoveEmptyEntries);

    private static string ResolveLink(string path)
    {
        var directory = new DirectoryInfo(path);
        if (directory.Exists)
        {
            return directory.ResolveLinkTarget(returnFinalTarget: true)?.FullName
                ?? directory.FullName;
        }

        var file = new FileInfo(path);
        return file.Exists
            ? file.ResolveLinkTarget(returnFinalTarget: true)?.FullName ?? file.FullName
            : directory.FullName;
    }

    private static bool IsWithin(string root, string path)
    {
        var relative = Path.GetRelativePath(root, path);
        return relative != ".."
            && !relative.StartsWith(
                $"..{Path.DirectorySeparatorChar}",
                StringComparison.Ordinal)
            && !Path.IsPathRooted(relative);
    }
}
