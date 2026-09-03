using System.Collections.Concurrent;
using System.Threading.Channels;
using App.GitHealth.Api.Persistence.Models;
using App.GitHealth.Api.Persistence.Repositories;
using App.GitHealth.Core.Common;
using Microsoft.Extensions.Options;

namespace App.GitHealth.Api.Features.Analyses;

internal sealed class AnalysisQueue : IDisposable
{
    /// <summary>
    /// Endings kept readable after the fact. A launch measures every baseline of a project
    /// at once, so the window has to hold a whole fan-out and not just the last run.
    /// </summary>
    private const int RetainedEndedRuns = 8;

    /// <summary>
    /// Keyed by baseline, not by project: the baselines of one project are separate
    /// measurements and must be allowed to run together.
    /// </summary>
    private readonly Dictionary<AnalysisTarget, Guid> _activeByTarget = [];

    private readonly Queue<Guid> _ended = new();
    private readonly Lock _endedGate = new();

    private readonly Channel<AnalysisWorkItem> _channel;
    private readonly IClock _clock;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly ConcurrentDictionary<Guid, AnalysisRunProgress> _progress = new();
    private readonly HashSet<Guid> _reservedProjects = [];
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly TimeSpan _timeout;

    public AnalysisQueue(
        IServiceScopeFactory scopeFactory,
        IClock clock,
        IOptions<AnalysisQueueOptions> options)
    {
        _scopeFactory = scopeFactory;
        _clock = clock;
        _timeout = TimeSpan.FromSeconds(options.Value.TimeoutSeconds);
        MaximumParallelAnalyses = options.Value.MaximumParallelAnalyses;
        _channel = Channel.CreateBounded<AnalysisWorkItem>(new BoundedChannelOptions(
            options.Value.Capacity)
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = MaximumParallelAnalyses == 1,
            SingleWriter = false,
        });
    }

    public DateTimeOffset UtcNow => _clock.UtcNow;

    public TimeSpan Timeout => _timeout;

    /// <summary>Number of queue readers, and therefore of analyses run in parallel.</summary>
    public int MaximumParallelAnalyses { get; }

    public async Task<AnalysisEnqueueResult> EnqueueAsync(
        AnalysisTarget target,
        CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            if (_activeByTarget.TryGetValue(target, out var activeId))
            {
                return new AnalysisEnqueueResult(
                    AnalysisEnqueueKind.Duplicate,
                    activeId,
                    IsDuplicate: true)
                { ReferenceName = target.ReferenceName };
            }

            if (_reservedProjects.Contains(target.ProjectId))
            {
                return new AnalysisEnqueueResult(
                    AnalysisEnqueueKind.ProjectBusy,
                    AnalysisId: null,
                    IsDuplicate: false)
                { ReferenceName = target.ReferenceName };
            }

            return await EnqueueLockedAsync(target, cancellationToken);
        }
        finally
        {
            _gate.Release();
        }
    }

    public IAsyncEnumerable<AnalysisWorkItem> ReadAllAsync(
        CancellationToken cancellationToken) => _channel.Reader.ReadAllAsync(cancellationToken);

    public async Task<ProjectOperationReservation?> TryReserveProjectAsync(
        Guid projectId,
        CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            var isBusy = _activeByTarget.Keys.Any(key => key.ProjectId == projectId);
            if (isBusy || !_reservedProjects.Add(projectId))
            {
                return null;
            }

            return new ProjectOperationReservation(this, projectId);
        }
        finally
        {
            _gate.Release();
        }
    }

    public bool TryRead(out AnalysisWorkItem? item) => _channel.Reader.TryRead(out item);

    public bool TryGetProgress(Guid analysisId, out AnalysisProgressSnapshot? progress)
    {
        if (_progress.TryGetValue(analysisId, out var live))
        {
            progress = live.Snapshot(_clock.UtcNow);
            return true;
        }

        progress = null;
        return false;
    }

    /// <summary>Live state of a run, created on first mention and released by <see cref="Forget"/>.</summary>
    public AnalysisRunProgress Track(Guid analysisId) =>
        _progress.GetOrAdd(analysisId, _ => new AnalysisRunProgress());

    public void Update(Guid analysisId, AnalysisPhase phase, string? message = null) =>
        Track(analysisId).SetPhase(phase, message);

    public async Task ReleaseAsync(AnalysisTarget target)
    {
        await _gate.WaitAsync();
        try
        {
            _activeByTarget.Remove(target);
        }
        finally
        {
            _gate.Release();
        }
    }

    public void Complete() => _channel.Writer.TryComplete();

    /// <summary>
    /// A run ends between two polls. Dropping its reading right away would leave a reader
    /// with a half-filled ledger under a header saying the whole thing was read, so the
    /// last few endings stay legible and are pushed out by the runs that follow.
    /// </summary>
    public void Forget(Guid analysisId)
    {
        lock (_endedGate)
        {
            _ended.Enqueue(analysisId);
            while (_ended.Count > RetainedEndedRuns)
            {
                _progress.TryRemove(_ended.Dequeue(), out _);
            }
        }
    }

    public void Dispose() => _gate.Dispose();

    internal async ValueTask ReleaseReservationAsync(Guid projectId)
    {
        await _gate.WaitAsync();
        try
        {
            _reservedProjects.Remove(projectId);
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task<AnalysisEnqueueResult> EnqueueLockedAsync(
        AnalysisTarget target,
        CancellationToken cancellationToken)
    {
        var analysisId = await StartAnalysisAsync(target, cancellationToken);
        if (analysisId is null)
        {
            return new AnalysisEnqueueResult(
                AnalysisEnqueueKind.ProjectNotFound,
                AnalysisId: null,
                IsDuplicate: false)
            { ReferenceName = target.ReferenceName };
        }

        var workItem = PrepareWork(target, analysisId.Value);
        if (_channel.Writer.TryWrite(workItem))
        {
            return Accepted(target, analysisId.Value);
        }

        await FailQueueFullAsync(workItem);
        _activeByTarget.Remove(target);
        Forget(analysisId.Value);
        return new AnalysisEnqueueResult(
            AnalysisEnqueueKind.QueueFull,
            analysisId,
            IsDuplicate: false)
        { ReferenceName = target.ReferenceName };
    }

    private AnalysisWorkItem PrepareWork(AnalysisTarget target, Guid analysisId)
    {
        _activeByTarget[target] = analysisId;
        Update(analysisId, AnalysisPhase.Waiting);
        return new AnalysisWorkItem(analysisId, target);
    }

    private static AnalysisEnqueueResult Accepted(AnalysisTarget target, Guid analysisId) => new(
        AnalysisEnqueueKind.Accepted,
        analysisId,
        IsDuplicate: false)
    { ReferenceName = target.ReferenceName };

    private async Task<Guid?> StartAnalysisAsync(
        AnalysisTarget target,
        CancellationToken cancellationToken)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var repository = scope.ServiceProvider.GetRequiredService<IAnalysisRepository>();
        try
        {
            return await repository.StartAsync(target, _clock.UtcNow, cancellationToken);
        }
        catch (Exception exception)
            when (exception is KeyNotFoundException or InvalidOperationException)
        {
            return null;
        }
    }

    private async Task FailQueueFullAsync(AnalysisWorkItem item)
    {
        Update(item.AnalysisId, AnalysisPhase.Failed, "The analysis queue is full.");
        await using var scope = _scopeFactory.CreateAsyncScope();
        var repository = scope.ServiceProvider.GetRequiredService<IAnalysisRepository>();
        var failure = new AnalysisFailure(
            "analysis.queue_full",
            "The analysis queue is full.",
            _clock.UtcNow);
        await repository.FailAsync(item.AnalysisId, failure, CancellationToken.None);
    }
}
