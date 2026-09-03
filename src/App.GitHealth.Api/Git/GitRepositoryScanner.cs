using System.IO.Enumeration;
using App.GitHealth.Api.Git.Models;
using App.GitHealth.Api.Git.Process;
using App.GitHealth.Api.Git.Scanning;
using App.GitHealth.Core.Analysis;
using App.GitHealth.Core.Branches;
using App.GitHealth.Core.Common;
using Microsoft.Extensions.Options;

namespace App.GitHealth.Api.Git;

internal sealed class GitRepositoryScanner : IRepositoryScanner
{
    private readonly IClock _clock;
    private readonly GitContributorReader _contributors;
    private readonly GitScannerOptions _options;
    private readonly IGitProcessRunner _runner;

    public GitRepositoryScanner(
        IGitProcessRunner runner,
        IOptions<GitScannerOptions> options,
        IClock clock)
    {
        _runner = runner;
        _options = options.Value;
        _clock = clock;
        _contributors = new GitContributorReader();
    }

    public async Task<RepositoryResult<RepositoryDescriptor>> InspectAsync(
        string repositoryPath,
        CancellationToken cancellationToken)
    {
        try
        {
            var repository = await CaptureAsync(repositoryPath, cancellationToken);
            return RepositoryResults.Success(ToDescriptor(repository));
        }
        catch (GitProcessException exception)
        {
            return RepositoryResults.Failure<RepositoryDescriptor>(
                new RepositoryError(exception.Code, exception.Message));
        }
        catch (Exception exception) when (exception is ArgumentException
            or IOException or UnauthorizedAccessException)
        {
            return RepositoryResults.Failure<RepositoryDescriptor>(
                new RepositoryError(RepositoryErrorCode.PathNotFound, exception.Message));
        }
    }

    public async Task<RepositoryResult<bool>> ContainsCommitAsync(
        string repositoryPath,
        CommitId commit,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(commit);
        try
        {
            var repository = await CaptureAsync(repositoryPath, cancellationToken);
            var isPresent = await ContainsCapturedCommitAsync(
                repository,
                commit,
                cancellationToken);
            return RepositoryResults.Success(isPresent);
        }
        catch (GitProcessException exception)
        {
            return RepositoryResults.Failure<bool>(
                new RepositoryError(exception.Code, exception.Message));
        }
        catch (Exception exception) when (exception is ArgumentException
            or IOException or UnauthorizedAccessException)
        {
            return RepositoryResults.Failure<bool>(
                new RepositoryError(RepositoryErrorCode.MalformedOutput, exception.Message));
        }
    }

    public async Task<RepositoryResult<RepositoryScan>> ScanAsync(
        RepositoryScanRequest request,
        CancellationToken cancellationToken)
    {
        return await ScanAsync(request, ScanReporter.Silent, cancellationToken);
    }

    public async Task<RepositoryResult<RepositoryScan>> ScanAsync(
        RepositoryScanRequest request,
        IProgress<RepositoryScanEvent> progress,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(progress);
        return await ScanAsync(request, new ScanReporter(progress), cancellationToken);
    }

    private async Task<RepositoryResult<RepositoryScan>> ScanAsync(
        RepositoryScanRequest request,
        ScanReporter reporter,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        try
        {
            var runner = reporter.IsFollowed
                ? new TracedGitProcessRunner(_runner, reporter)
                : _runner;
            var execution = new RepositoryScanExecution(request, reporter, runner);
            var repository = await CaptureAsync(
                runner,
                request.RepositoryPath,
                cancellationToken);
            var scan = await ScanCapturedAsync(repository, execution, cancellationToken);
            return RepositoryResults.Success(scan);
        }
        catch (GitProcessException exception)
        {
            return RepositoryResults.Failure<RepositoryScan>(
                new RepositoryError(exception.Code, exception.Message));
        }
        catch (Exception exception) when (exception is ArgumentException
            or IOException or UnauthorizedAccessException)
        {
            return RepositoryResults.Failure<RepositoryScan>(
                new RepositoryError(RepositoryErrorCode.MalformedOutput, exception.Message));
        }
    }

    private Task<CapturedRepository> CaptureAsync(
        string repositoryPath,
        CancellationToken cancellationToken) =>
        CaptureAsync(_runner, repositoryPath, cancellationToken);

    private Task<CapturedRepository> CaptureAsync(
        IGitProcessRunner runner,
        string repositoryPath,
        CancellationToken cancellationToken)
    {
        var request = new GitRepositoryCaptureRequest(
            repositoryPath,
            _options.RepositoriesRoot);
        return GitRepositoryReader.CaptureAsync(runner, request, cancellationToken);
    }

    private async Task<bool> ContainsCapturedCommitAsync(
        CapturedRepository repository,
        CommitId commit,
        CancellationToken cancellationToken)
    {
        var expression = $"{commit.Value}^{{commit}}";
        var arguments = new[]
        {
            "-C", repository.Context.InvocationPath, "cat-file", "-e", "--", expression,
        };
        var command = GitCommand.CreateRepository(
            repository.Context.InvocationPath,
            arguments);
        var result = await _runner.RunAsync(command, cancellationToken);
        return result.ExitCode == 0;
    }

    private async Task<RepositoryScan> ScanCapturedAsync(
        CapturedRepository repository,
        RepositoryScanExecution execution,
        CancellationToken cancellationToken)
    {
        var request = execution.Request;
        var reporter = execution.Reporter;
        var reference = FindReference(repository, request.Reference);
        var branches = SelectBranches(repository, request, reference);
        reporter.ReferencesListed(branches.Select(ToListing).ToArray());
        var topologyReader = new GitTopologyReader(execution.Runner, _options, reporter);
        var topologyScan = new TopologyScan(repository, reference, branches);
        reporter.StageStarted(RepositoryScanStage.Topology);
        var topology = await topologyReader.ReadAsync(topologyScan, cancellationToken);
        reporter.StageStarted(RepositoryScanStage.Enrichment);
        var scanned = await EnrichAsync(execution, topologyScan, topology, cancellationToken);
        var metadata = new RepositoryScanMetadata(_clock.UtcNow, repository.GitVersion);
        return new RepositoryScan(metadata, reference.Commit, scanned);
    }

    private async Task<IReadOnlyList<ScannedBranch>> EnrichAsync(
        RepositoryScanExecution execution,
        TopologyScan scan,
        IReadOnlyDictionary<string, BranchDivergence> topology,
        CancellationToken cancellationToken)
    {
        var reporter = execution.Reporter;
        var scanned = new List<ScannedBranch>(scan.Branches.Count);
        foreach (var branch in scan.Branches)
        {
            reporter.ReferenceStarted(branch.Reference, RepositoryScanStage.Enrichment);
            var divergence = topology[branch.Reference.FullName];
            var comparison = new GitComparison(
                scan.Repository.Context,
                scan.Reference.Commit,
                branch.Commit);
            var contributors = divergence.AheadCount == 0
                ? []
                : await _contributors.ReadAsync(execution.Runner, comparison, cancellationToken);
            reporter.ReferenceEnriched(branch.Reference, contributors);
            scanned.Add(ToScannedBranch(branch, divergence, contributors));
        }

        return scanned;
    }

    private static ScannedReferenceListing ToListing(CapturedReference branch) => new()
    {
        ReferenceName = branch.Reference.FullName,
        CommitId = branch.Commit.Value,
        LastActivityAtUtc = branch.LastActivityAt,
        TipAuthor = branch.TipAuthor,
    };

    private static CapturedReference FindReference(
        CapturedRepository repository,
        GitRef reference)
    {
        if (repository.References.TryGetValue(reference.FullName, out var captured)
            && captured.SymbolicTarget is null)
        {
            return captured;
        }

        throw new GitProcessException(
            RepositoryErrorCode.InvalidReference,
            "The chosen baseline does not exist in the repository.");
    }

    private static List<CapturedReference> SelectBranches(
        CapturedRepository repository,
        RepositoryScanRequest request,
        CapturedReference reference)
    {
        return repository.References.Values
            .Where(branch => branch.SymbolicTarget is null)
            .Where(branch => branch.Reference != reference.Reference)
            .Where(branch => FileSystemName.MatchesSimpleExpression(
                request.BranchPattern,
                branch.Reference.FullName,
                ignoreCase: false))
            .OrderBy(branch => branch.Reference.FullName, StringComparer.Ordinal)
            .ToList();
    }

    private static RepositoryDescriptor ToDescriptor(CapturedRepository repository)
    {
        var location = new RepositoryLocation(
            repository.Context.CanonicalPath,
            repository.Context.GitDirectory,
            repository.Context.WorkingTreePath);
        var references = repository.References.Values
            .Where(reference => reference.SymbolicTarget is null)
            .Select(reference => reference.Reference)
            .OrderBy(reference => reference.FullName, StringComparer.Ordinal)
            .ToArray();
        return new RepositoryDescriptor(location, SuggestReference(repository), references);
    }

    private static GitRef? SuggestReference(CapturedRepository repository)
    {
        const string originHead = "refs/remotes/origin/HEAD";
        if (repository.References.TryGetValue(originHead, out var symbolic)
            && symbolic.SymbolicTarget is not null
            && repository.References.TryGetValue(symbolic.SymbolicTarget, out var target))
        {
            return target.Reference;
        }

        var candidates = new[]
        {
            "refs/heads/main",
            "refs/remotes/origin/main",
            "refs/heads/master",
            "refs/remotes/origin/master",
        };
        return candidates
            .Select(candidate => repository.References.GetValueOrDefault(candidate))
            .FirstOrDefault(reference => reference?.SymbolicTarget is null)
            ?.Reference;
    }

    private static ScannedBranch ToScannedBranch(
        CapturedReference branch,
        BranchDivergence divergence,
        IReadOnlyList<Contributor> contributors)
    {
        var tip = new BranchTip(branch.Commit, branch.LastActivityAt, branch.TipAuthor);
        var facts = new BranchFacts(branch.Reference, divergence, tip);
        return new ScannedBranch(facts, contributors);
    }
}
