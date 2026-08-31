using App.GitHealth.Api.Git.Process;
using App.GitHealth.Core.Analysis;

namespace App.GitHealth.Api.Git.Paths;

internal static class GitObjectDatabaseGuard
{
    private const int MaximumAlternateCount = 64;
    private const int MaximumAlternateFileBytes = 64 * 1024;
    private const int MaximumAlternatePathLength = 32768;

    public static async Task EnsureAllowedAsync(
        string? repositoriesRoot,
        string objectDirectory,
        CancellationToken cancellationToken)
    {
        var pending = new Stack<string>();
        var visited = new HashSet<string>(PathComparer());
        pending.Push(objectDirectory);
        while (pending.TryPop(out var candidate))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var current = ResolveAllowed(repositoriesRoot, candidate);
            if (!visited.Add(current))
            {
                continue;
            }

            EnsureWithinTraversalBudget(visited.Count);
            var alternates = await ReadAlternatesAsync(
                repositoriesRoot,
                current,
                cancellationToken);
            foreach (var alternate in alternates)
            {
                pending.Push(alternate);
            }
        }
    }

    private static async Task<IReadOnlyList<string>> ReadAlternatesAsync(
        string? repositoriesRoot,
        string objectDirectory,
        CancellationToken cancellationToken)
    {
        var configuredPath = Path.Combine(objectDirectory, "info", "alternates");
        if (!File.Exists(configuredPath))
        {
            return [];
        }

        var safePath = ResolveAllowed(repositoriesRoot, configuredPath);
        await using var stream = OpenBounded(safePath);
        using var reader = new StreamReader(stream);
        var alternates = new List<string>();
        while (await reader.ReadLineAsync(cancellationToken) is { } line)
        {
            EnsureValidLine(line, alternates.Count);
            if (line.Length > 0)
            {
                alternates.Add(ResolveAlternate(objectDirectory, line));
            }
        }

        return alternates;
    }

    private static FileStream OpenBounded(string path)
    {
        var stream = new FileStream(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            bufferSize: 4096,
            FileOptions.Asynchronous | FileOptions.SequentialScan);
        if (stream.Length <= MaximumAlternateFileBytes)
        {
            return stream;
        }

        stream.Dispose();
        throw UnsafeObjectDatabase();
    }

    private static void EnsureValidLine(string line, int alternateCount)
    {
        if (line.Length > MaximumAlternatePathLength
            || alternateCount >= MaximumAlternateCount
            || line.StartsWith('"'))
        {
            throw UnsafeObjectDatabase();
        }
    }

    private static string ResolveAlternate(string objectDirectory, string alternate)
    {
        try
        {
            return Path.IsPathFullyQualified(alternate)
                ? Path.GetFullPath(alternate)
                : Path.GetFullPath(alternate, objectDirectory);
        }
        catch (Exception exception) when (exception is ArgumentException
            or NotSupportedException or PathTooLongException)
        {
            throw UnsafeObjectDatabase(exception);
        }
    }

    private static string ResolveAllowed(string? repositoriesRoot, string path)
    {
        try
        {
            var physicalPath = RepositoryPathGuard.ResolvePhysicalPath(path);
            return RepositoryPathGuard.IsAllowed(repositoriesRoot, physicalPath)
                ? physicalPath
                : throw UnsafeObjectDatabase();
        }
        catch (GitProcessException)
        {
            throw;
        }
        catch (Exception exception) when (exception is ArgumentException
            or IOException or UnauthorizedAccessException)
        {
            throw UnsafeObjectDatabase(exception);
        }
    }

    private static void EnsureWithinTraversalBudget(int databaseCount)
    {
        if (databaseCount > MaximumAlternateCount)
        {
            throw UnsafeObjectDatabase();
        }
    }

    private static StringComparer PathComparer() => OperatingSystem.IsWindows()
        ? StringComparer.OrdinalIgnoreCase
        : StringComparer.Ordinal;

    private static GitProcessException UnsafeObjectDatabase(Exception? inner = null) => new(
        RepositoryErrorCode.PathNotAllowed,
        "A Git object database is outside the allowed root or is invalid.",
        inner);
}
