using System.Security;
using App.GitHealth.Api.Git.Paths;

namespace App.GitHealth.Api.Features.Discovery;

/// <summary>
/// Locates the Git repositories contained in a tree. The walk stops as soon as a repository
/// is recognised: its nested submodules and worktrees are therefore never offered
/// separately.
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

    /// <summary>Deep folders of no interest to the detection, skipped before descending.</summary>
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

    /// <summary>A hidden or build folder contains no repository to offer.</summary>
    private static bool IsSkipped(string name) =>
        name.StartsWith('.') || SkippedDirectoryNames.Contains(name, StringComparer.Ordinal);

    private static bool IsRepository(string path)
    {
        // A linked worktree and a submodule carry a `.git` file, not a folder.
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
