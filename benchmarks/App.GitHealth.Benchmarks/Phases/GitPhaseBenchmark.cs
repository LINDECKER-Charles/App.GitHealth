using App.GitHealth.Api.Git;
using App.GitHealth.Api.Git.Models;
using App.GitHealth.Api.Git.Process;
using App.GitHealth.Api.Git.Scanning;
using App.GitHealth.Core.Analysis;
using App.GitHealth.Core.Branches;
using Microsoft.Extensions.Options;

namespace App.GitHealth.Benchmarks.Phases;

internal sealed class GitPhaseBenchmark
{
    private static readonly DateTimeOffset CaptureTime =
        new(2026, 8, 29, 12, 0, 0, TimeSpan.Zero);
    private readonly IReadOnlyList<CapturedReference> _branches;
    private readonly CapturedRepository _repository;
    private readonly CapturedReference _reference;
    private readonly IGitProcessRunner _runner;
    private readonly GitScannerOptions _settings;

    private GitPhaseBenchmark(
        GitPhaseContext context,
        IGitProcessRunner runner,
        GitScannerOptions settings)
    {
        _repository = context.Repository;
        _reference = context.Reference;
        _branches = context.Branches;
        _runner = runner;
        _settings = settings;
    }

    public static async Task<GitPhaseBenchmark> CreateAsync(
        string repositoryPath,
        int expectedBranchCount,
        CancellationToken cancellationToken)
    {
        var settings = new GitScannerOptions
        {
            CommandTimeout = TimeSpan.FromMinutes(2),
            MaximumOutputBytes = 16 * 1024 * 1024,
            MaximumParallelCommands = 4,
            UseAheadBehind = true,
        };
        var runner = new GitProcessRunner(
            Options.Create(settings),
            GitExecutableResolver.Capture(settings.ExecutablePath));
        var repository = await GitRepositoryReader.CaptureAsync(
            runner,
            new GitRepositoryCaptureRequest(repositoryPath, RepositoriesRoot: null),
            cancellationToken);
        var context = new GitPhaseContext(
            repository,
            repository.References["refs/heads/main"],
            SelectBranches(repository, expectedBranchCount));
        return new GitPhaseBenchmark(context, runner, settings);
    }

    private static CapturedReference[] SelectBranches(
        CapturedRepository repository,
        int expectedBranchCount)
    {
        var branches = repository.References.Values
            .Where(item => item.Reference.FullName.StartsWith(
                "refs/remotes/origin/benchmark/",
                StringComparison.Ordinal))
            .OrderBy(item => item.Reference.FullName, StringComparer.Ordinal)
            .ToArray();
        if (branches.Length != expectedBranchCount)
        {
            throw new InvalidOperationException(
                $"Fixture invalide : {branches.Length} branches au lieu de " +
                $"{expectedBranchCount}.");
        }

        return branches;
    }

    public Task<IReadOnlyDictionary<string, BranchDivergence>> ReadTopologyAsync(
        CancellationToken cancellationToken)
    {
        var scan = new TopologyScan(_repository, _reference, _branches);
        return new GitTopologyReader(_runner, _settings).ReadAsync(scan, cancellationToken);
    }

    public async Task<RepositoryScan> EnrichAsync(
        IReadOnlyDictionary<string, BranchDivergence> topology,
        CancellationToken cancellationToken)
    {
        var contributorReader = new GitContributorReader(_runner);
        var branches = new List<ScannedBranch>(_branches.Count);
        foreach (var branch in _branches)
        {
            var divergence = topology[branch.Reference.FullName];
            var comparison = new GitComparison(
                _repository.Context,
                _reference.Commit,
                branch.Commit);
            var contributors = await contributorReader.ReadAsync(comparison, cancellationToken);
            var facts = new BranchFacts(
                branch.Reference,
                divergence,
                new BranchTip(branch.Commit, branch.LastActivityAt, branch.TipAuthor));
            branches.Add(new ScannedBranch(facts, contributors));
        }

        var metadata = new RepositoryScanMetadata(CaptureTime, _repository.GitVersion);
        return new RepositoryScan(metadata, _reference.Commit, branches);
    }

    private sealed record GitPhaseContext(
        CapturedRepository Repository,
        CapturedReference Reference,
        IReadOnlyList<CapturedReference> Branches);
}
