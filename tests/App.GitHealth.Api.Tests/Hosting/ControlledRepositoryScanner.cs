using System.Collections.Concurrent;
using App.GitHealth.Core.Analysis;
using App.GitHealth.Core.Branches;

namespace App.GitHealth.Api.Tests.Hosting;

public sealed class ControlledRepositoryScanner(
    RepositoryScan scan,
    RepositoryDescriptor? descriptor = null) : IRepositoryScanner
{
    private readonly TaskCompletionSource _advance = CreateSignal();
    private readonly ConcurrentDictionary<int, TaskCompletionSource> _concurrency = new();
    private readonly TaskCompletionSource _enrichment = CreateSignal();
    private readonly TaskCompletionSource _finish = CreateSignal();
    private readonly ConcurrentQueue<RepositoryScanRequest> _requests = new();
    private readonly TaskCompletionSource _started = CreateSignal();
    private int _activeScans;
    private int _peakScans;

    public Task EnrichmentStarted => _enrichment.Task;

    public Task Started => _started.Task;

    /// <summary>Nombre maximal d'analyses observées en même temps depuis le démarrage.</summary>
    public int PeakConcurrency => Volatile.Read(ref _peakScans);

    public IReadOnlyCollection<RepositoryScanRequest> Requests => _requests.ToArray();

    /// <summary>Se termine dès que le nombre demandé d'analyses est mené de front.</summary>
    public Task ReachedConcurrency(int scanCount)
    {
        var signal = _concurrency.GetOrAdd(scanCount, _ => CreateSignal());
        if (PeakConcurrency >= scanCount)
        {
            signal.TrySetResult();
        }

        return signal.Task;
    }

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
        TrackScanStart();
        try
        {
            progress.Report(RepositoryScanStage.Topology);
            _started.TrySetResult();
            await _advance.Task.WaitAsync(cancellationToken);
            progress.Report(RepositoryScanStage.Enrichment);
            _enrichment.TrySetResult();
            await _finish.Task.WaitAsync(cancellationToken);
        }
        finally
        {
            Interlocked.Decrement(ref _activeScans);
        }

        return RepositoryResults.Success(scan);
    }

    public void AdvanceToEnrichment() => _advance.TrySetResult();

    public void Release()
    {
        _advance.TrySetResult();
        _finish.TrySetResult();
    }

    private void TrackScanStart()
    {
        var active = Interlocked.Increment(ref _activeScans);
        RaisePeak(ref _peakScans, active);
        foreach (var expectation in _concurrency.Where(entry => entry.Key <= active))
        {
            expectation.Value.TrySetResult();
        }
    }

    private static void RaisePeak(ref int peak, int observed)
    {
        var current = Volatile.Read(ref peak);
        while (observed > current)
        {
            var previous = Interlocked.CompareExchange(ref peak, observed, current);
            if (previous == current)
            {
                return;
            }

            current = previous;
        }
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
