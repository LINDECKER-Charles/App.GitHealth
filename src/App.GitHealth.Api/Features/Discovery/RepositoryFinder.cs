using System.Security;
using App.GitHealth.Api.Git.Paths;

namespace App.GitHealth.Api.Features.Discovery;

/// <summary>
/// Repère les dépôts Git contenus dans une arborescence. Le parcours s'arrête dès qu'un dépôt
/// est reconnu : ses sous-modules et ses worktrees imbriqués ne sont donc jamais proposés
/// séparément.
/// </summary>
internal static class RepositoryFinder
{
    internal const int DefaultDepth = 3;
    internal const int MinimumDepth = 1;
    internal const int MaximumDepth = 8;
    internal const int MaximumResults = 200;

    private const string GitMetadataName = ".git";
    private const string BareHeadName = "HEAD";
    private const string BareObjectsName = "objects";
    private const string BareReferencesName = "refs";

    /// <summary>Dossiers profonds sans intérêt pour la détection, écartés avant descente.</summary>
    private static readonly string[] SkippedDirectoryNames =
    [
        "node_modules",
        "bin",
        "obj",
        "target",
        "vendor",
        "dist",
        "build",
        "__pycache__",
    ];

    public static RepositorySearch Find(string rootPath, int depth, string? allowedRoot)
    {
        var found = new List<string>();
        var pending = new Queue<SearchEntry>();
        pending.Enqueue(new SearchEntry(new DirectoryInfo(rootPath), 0));
        var isTruncated = false;
        while (pending.Count > 0)
        {
            if (found.Count == MaximumResults)
            {
                isTruncated = true;
                break;
            }

            Visit(pending.Dequeue(), found, new SearchBounds(depth, allowedRoot, pending));
        }

        found.Sort(StringComparer.Ordinal);
        return new RepositorySearch(found, isTruncated);
    }

    public static int ClampDepth(int? requestedDepth) => requestedDepth is null
        ? DefaultDepth
        : Math.Clamp(requestedDepth.Value, MinimumDepth, MaximumDepth);

    private static void Visit(SearchEntry entry, List<string> found, SearchBounds bounds)
    {
        if (IsRepository(entry.Directory.FullName))
        {
            found.Add(entry.Directory.FullName);
            return;
        }

        if (entry.Depth < bounds.Depth)
        {
            EnqueueChildren(entry, bounds);
        }
    }

    private static void EnqueueChildren(SearchEntry entry, SearchBounds bounds)
    {
        foreach (var child in ReadAccessibleDirectories(entry.Directory))
        {
            if (IsSkipped(child.Name)
                || !RepositoryPathGuard.IsAllowed(bounds.AllowedRoot, child.FullName))
            {
                continue;
            }

            bounds.Pending.Enqueue(new SearchEntry(child, entry.Depth + 1));
        }
    }

    private static DirectoryInfo[] ReadAccessibleDirectories(DirectoryInfo directory)
    {
        try
        {
            return directory.EnumerateDirectories().ToArray();
        }
        catch (Exception exception) when (exception is UnauthorizedAccessException
            or SecurityException or IOException or ArgumentException or NotSupportedException)
        {
            return [];
        }
    }

    /// <summary>Un dossier caché ou de build ne contient pas de dépôt à proposer.</summary>
    private static bool IsSkipped(string name) =>
        name.StartsWith('.') || SkippedDirectoryNames.Contains(name, StringComparer.Ordinal);

    private static bool IsRepository(string path)
    {
        // Un worktree lié et un sous-module portent un fichier `.git`, pas un dossier.
        var metadataPath = Path.Combine(path, GitMetadataName);
        return Directory.Exists(metadataPath)
            || File.Exists(metadataPath)
            || IsBareRepository(path);
    }

    private static bool IsBareRepository(string path) =>
        File.Exists(Path.Combine(path, BareHeadName))
        && Directory.Exists(Path.Combine(path, BareObjectsName))
        && Directory.Exists(Path.Combine(path, BareReferencesName));

    private sealed record SearchEntry(DirectoryInfo Directory, int Depth);

    private sealed record SearchBounds(
        int Depth,
        string? AllowedRoot,
        Queue<SearchEntry> Pending);
}
