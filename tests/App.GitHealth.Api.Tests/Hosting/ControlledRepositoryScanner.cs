using System.Collections.Concurrent;
using App.GitHealth.Core.Analysis;
using App.GitHealth.Core.Branches;

namespace App.GitHealth.Api.Tests.Hosting;

public sealed class ControlledRepositoryScanner(
    RepositoryScan scan,
    RepositoryDescriptor? descriptor = null) : IRepositoryScanner
{
    private readonly TaskCompletionSource _advance = CreateSignal();
    private readonly TaskCompletionSource _enrichment = CreateSignal();
    private readonly TaskCompletionSource _finish = CreateSignal();
    private readonly ConcurrentQueue<RepositoryScanRequest> _requests = new();
    private readonly TaskCompletionSource _started = CreateSignal();

    public Task EnrichmentStarted => _enrichment.Task;

    public Task Started => _started.Task;

    public IReadOnlyCollection<RepositoryScanRequest> Requests => _requests.ToArray();

    public Task<RepositoryResult<RepositoryDescriptor>> InspectAsync(
        string repositoryPath,
        CancellationToken cancellationToken)
    {
        _ = repositoryPath;
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(RepositoryResults.Success(
            descriptor ?? CreateDescriptor(repositoryPath)));
    }

    public Task<RepositoryResult<bool>> ContainsCommitAsync(
        string repositoryPath,
        CommitId commit,
        CancellationToken cancellationToken)
    {
        _ = repositoryPath;
        _ = commit;
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(RepositoryResults.Success(true));
    }

    public Task<RepositoryResult<RepositoryScan>> ScanAsync(
        RepositoryScanRequest request,
        CancellationToken cancellationToken) =>
        ScanAsync(request, new Progress<RepositoryScanStage>(), cancellationToken);

    public async Task<RepositoryResult<RepositoryScan>> ScanAsync(
        RepositoryScanRequest request,
        IProgress<RepositoryScanStage> progress,
        CancellationToken cancellationToken)
    {
        _requests.Enqueue(request);
        progress.Report(RepositoryScanStage.Topology);
        _started.TrySetResult();
        await _advance.Task.WaitAsync(cancellationToken);
        progress.Report(RepositoryScanStage.Enrichment);
        _enrichment.TrySetResult();
        await _finish.Task.WaitAsync(cancellationToken);
        return RepositoryResults.Success(scan);
    }

    public void AdvanceToEnrichment() => _advance.TrySetResult();

    public void Release()
    {
        _advance.TrySetResult();
        _finish.TrySetResult();
    }

    private static TaskCompletionSource CreateSignal() =>
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    private static RepositoryDescriptor CreateDescriptor(string repositoryPath)
    {
        var canonicalPath = Path.GetFullPath(repositoryPath);
        var reference = new GitRef("refs/heads/main");
        var alternate = new GitRef("refs/heads/develop");
        var location = new RepositoryLocation(
            canonicalPath,
            Path.Combine(canonicalPath, ".git"),
            canonicalPath);
        return new RepositoryDescriptor(location, reference, [reference, alternate]);
    }
}
