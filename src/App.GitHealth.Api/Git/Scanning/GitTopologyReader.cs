using System.Collections.Concurrent;
using App.GitHealth.Api.Git.Models;
using App.GitHealth.Api.Git.Parsing;
using App.GitHealth.Api.Git.Process;
using App.GitHealth.Core.Analysis;
using App.GitHealth.Core.Branches;

namespace App.GitHealth.Api.Git.Scanning;

internal sealed class GitTopologyReader(
    IGitProcessRunner runner,
    GitScannerOptions options)
{
    public async Task<IReadOnlyDictionary<string, BranchDivergence>> ReadAsync(
        TopologyScan scan,
        CancellationToken cancellationToken)
    {
        if (options.UseAheadBehind)
        {
            var fastResult = await TryFastPathAsync(scan, cancellationToken);
            if (fastResult is not null)
            {
                return fastResult;
            }
        }

        return await ReadFallbackAsync(scan, cancellationToken);
    }

    private async Task<IReadOnlyDictionary<string, BranchDivergence>?> TryFastPathAsync(
        TopologyScan scan,
        CancellationToken cancellationToken)
    {
        var result = await RunFastPathAsync(scan, cancellationToken);
        if (result.ExitCode != 0)
        {
            return IsUnsupportedAheadBehind(result.StandardError)
                ? null
                : throw ProcessFailure("Git could not compute the topology.");
        }

        var current = GitOutputParser.ParseAheadBehind(result.StandardOutput);
        return await BuildFastPathAsync(scan, current, cancellationToken);
    }

    private Task<GitCommandResult> RunFastPathAsync(
        TopologyScan scan,
        CancellationToken cancellationToken)
    {
        var format = "%(refname)%00%(objectname)%00" +
            $"%(ahead-behind:{scan.Reference.Commit.Value})%00";
        return RunAsync(
            scan.Repository.Context,
            ["for-each-ref", "--sort=refname", $"--format={format}",
                "refs/heads", "refs/remotes"],
            cancellationToken);
    }

    private async Task<IReadOnlyDictionary<string, BranchDivergence>> BuildFastPathAsync(
        TopologyScan scan,
        IReadOnlyDictionary<string, (CommitId Commit, int Ahead, int Behind)> current,
        CancellationToken cancellationToken)
    {
        var topology = new Dictionary<string, BranchDivergence>(StringComparer.Ordinal);
        var moved = new List<CapturedReference>();
        foreach (var branch in scan.Branches)
        {
            if (!current.TryGetValue(branch.Reference.FullName, out var counts)
                || counts.Commit != branch.Commit)
            {
                moved.Add(branch);
                continue;
            }

            var comparison = CreateComparison(scan, branch);
            var aheadBehind = new AheadBehindCounts(counts.Ahead, counts.Behind);
            topology[branch.Reference.FullName] = await BuildDivergenceAsync(
                comparison,
                aheadBehind,
                cancellationToken);
        }

        var fallback = await ReadMovedReferencesAsync(scan, moved, cancellationToken);
        Merge(topology, fallback);
        return topology;
    }

    private async Task<IReadOnlyDictionary<string, BranchDivergence>> ReadMovedReferencesAsync(
        TopologyScan scan,
        List<CapturedReference> moved,
        CancellationToken cancellationToken)
    {
        if (moved.Count == 0)
        {
            return new Dictionary<string, BranchDivergence>();
        }

        var movedScan = new TopologyScan(scan.Repository, scan.Reference, moved);
        return await ReadFallbackAsync(movedScan, cancellationToken);
    }

    private async Task<IReadOnlyDictionary<string, BranchDivergence>> ReadFallbackAsync(
        TopologyScan scan,
        CancellationToken cancellationToken)
    {
        var result = new ConcurrentDictionary<string, BranchDivergence>(StringComparer.Ordinal);
        var parallelOptions = new ParallelOptions
        {
            CancellationToken = cancellationToken,
            MaxDegreeOfParallelism = options.MaximumParallelCommands,
        };
        await Parallel.ForEachAsync(scan.Branches, parallelOptions, async (branch, token) =>
        {
            var comparison = CreateComparison(scan, branch);
            result[branch.Reference.FullName] = await ReadFallbackBranchAsync(
                comparison,
                token);
        });
        return result;
    }

    private async Task<BranchDivergence> ReadFallbackBranchAsync(
        GitComparison comparison,
        CancellationToken cancellationToken)
    {
        var range = $"{comparison.Reference.Value}...{comparison.Branch.Value}";
        var result = await RunAsync(
            comparison.Context,
            ["rev-list", "--left-right", "--count", range, "--"],
            cancellationToken);
        if (result.ExitCode != 0)
        {
            throw ProcessFailure("Git could not compare two captured commits.");
        }

        var values = GitOutputParser.ParseRevListCounts(result.StandardOutput);
        var counts = new AheadBehindCounts(values.Ahead, values.Behind);
        return await BuildDivergenceAsync(comparison, counts, cancellationToken);
    }

    private async Task<BranchDivergence> BuildDivergenceAsync(
        GitComparison comparison,
        AheadBehindCounts counts,
        CancellationToken cancellationToken)
    {
        if (counts.Ahead == 0 && counts.Behind == 0)
        {
            return BranchDivergence.Create(0, 0, BranchRelationship.SameCommit);
        }

        if (counts.Ahead == 0)
        {
            return BranchDivergence.Create(
                counts.Ahead,
                counts.Behind,
                BranchRelationship.BranchIsAncestorOfReference);
        }

        var relationship = counts.Behind > 0
            && !await HasCommonAncestorAsync(comparison, cancellationToken)
            ? BranchRelationship.NoCommonAncestor
            : BranchRelationship.CommonAncestor;
        return BranchDivergence.Create(counts.Ahead, counts.Behind, relationship);
    }

    private async Task<bool> HasCommonAncestorAsync(
        GitComparison comparison,
        CancellationToken cancellationToken)
    {
        var result = await RunAsync(
            comparison.Context,
            ["merge-base", comparison.Reference.Value, comparison.Branch.Value],
            cancellationToken);
        if (result.ExitCode is 0 or 1)
        {
            return result.ExitCode == 0 && !string.IsNullOrWhiteSpace(result.StandardOutput);
        }

        throw ProcessFailure("Git could not determine the merge base.");
    }

    private Task<GitCommandResult> RunAsync(
        GitRepositoryContext context,
        IEnumerable<string> arguments,
        CancellationToken cancellationToken)
    {
        var commandArguments = new[] { "-C", context.InvocationPath }.Concat(arguments);
        var command = GitCommand.CreateRepository(
            context.InvocationPath,
            commandArguments);
        return runner.RunAsync(command, cancellationToken);
    }

    private static GitComparison CreateComparison(
        TopologyScan scan,
        CapturedReference branch)
    {
        return new GitComparison(
            scan.Repository.Context,
            scan.Reference.Commit,
            branch.Commit);
    }

    private static void Merge(
        Dictionary<string, BranchDivergence> target,
        IReadOnlyDictionary<string, BranchDivergence> source)
    {
        foreach (var item in source)
        {
            target[item.Key] = item.Value;
        }
    }

    private static GitProcessException ProcessFailure(string message) =>
        new(RepositoryErrorCode.ProcessFailed, message);

    private static bool IsUnsupportedAheadBehind(string error) =>
        error.Contains("ahead-behind", StringComparison.OrdinalIgnoreCase)
        && (error.Contains("unknown", StringComparison.OrdinalIgnoreCase)
            || error.Contains("malformed", StringComparison.OrdinalIgnoreCase));
}
