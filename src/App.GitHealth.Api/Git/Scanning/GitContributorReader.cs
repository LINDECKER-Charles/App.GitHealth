using System.Collections.Concurrent;
using App.GitHealth.Api.Git.Models;
using App.GitHealth.Api.Git.Parsing;
using App.GitHealth.Api.Git.Process;
using App.GitHealth.Core.Analysis;
using App.GitHealth.Core.Branches;

namespace App.GitHealth.Api.Git.Scanning;

/// <summary>
/// Reads who wrote the commits a branch adds. The runner comes per call: a followed scan
/// runs its commands through a traced one, while the cache stays shared across scans.
/// </summary>
internal sealed class GitContributorReader
{
    private readonly ConcurrentDictionary<string, IReadOnlyList<Contributor>> _cache = new();

    public async Task<IReadOnlyList<Contributor>> ReadAsync(
        IGitProcessRunner runner,
        GitComparison comparison,
        CancellationToken cancellationToken)
    {
        var cacheKey = BuildCacheKey(comparison);
        if (_cache.TryGetValue(cacheKey, out var cached))
        {
            return cached;
        }

        var command = CreateCommand(comparison);
        var result = await runner.RunAsync(command, cancellationToken);
        EnsureSuccess(result);

        var contributors = GitOutputParser.ParseContributors(result.StandardOutput);
        _cache.TryAdd(cacheKey, contributors);
        return contributors;
    }

    private static GitCommand CreateCommand(GitComparison comparison)
    {
        var range = $"{comparison.Reference.Value}..{comparison.Branch.Value}";
        var arguments = new[]
        {
            "-C", comparison.Context.InvocationPath,
            "log", "--no-merges", "--use-mailmap", "--no-show-signature",
            "--no-patch", "-z", "--format=%aN%x00%aE%x00", range, "--",
        };
        return GitCommand.CreateRepository(comparison.Context.InvocationPath, arguments);
    }

    private static void EnsureSuccess(GitCommandResult result)
    {
        if (result.ExitCode != 0)
        {
            throw new GitProcessException(
                RepositoryErrorCode.ProcessFailed,
                "Git could not read the branch contributors.");
        }
    }

    private static string BuildCacheKey(GitComparison comparison)
    {
        var mailmapSignature = "none";
        if (comparison.Context.WorkingTreePath is not null)
        {
            var mailmapPath = Path.Combine(comparison.Context.WorkingTreePath, ".mailmap");
            if (File.Exists(mailmapPath))
            {
                var info = new FileInfo(mailmapPath);
                mailmapSignature = $"{info.Length}:{info.LastWriteTimeUtc.Ticks}";
            }
        }

        return string.Join(
            '|',
            comparison.Context.GitDirectory,
            comparison.Reference.Value,
            comparison.Branch.Value,
            mailmapSignature);
    }
}
