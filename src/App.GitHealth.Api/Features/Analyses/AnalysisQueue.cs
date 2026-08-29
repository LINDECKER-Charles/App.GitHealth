using System.Collections.Concurrent;
using System.Threading.Channels;
using App.GitHealth.Api.Persistence.Models;
using App.GitHealth.Api.Persistence.Repositories;
using App.GitHealth.Core.Common;
using Microsoft.Extensions.Options;

namespace App.GitHealth.Api.Features.Analyses;

internal sealed class AnalysisQueue : IDisposable
{
    private readonly Dictionary<Guid, Guid> _activeByProject = [];
    private readonly Channel<AnalysisWorkItem> _channel;
    private readonly IClock _clock;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly ConcurrentDictionary<Guid, AnalysisProgressSnapshot> _progress = new();
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

    /// <summary>Nombre de lecteurs de la file, donc d'analyses menées de front.</summary>
    public int MaximumParallelAnalyses { get; }

    public async Task<AnalysisEnqueueResult> EnqueueAsync(
        Guid projectId,
        CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            if (_activeByProject.TryGetValue(projectId, out var activeId))
            {
                return new AnalysisEnqueueResult(
                    AnalysisEnqueueKind.Duplicate,
                    activeId,
                    IsDuplicate: true);
            }

            if (_reservedProjects.Contains(projectId))
            {
                return new AnalysisEnqueueResult(
                    AnalysisEnqueueKind.ProjectBusy,
                    AnalysisId: null,
                    IsDuplicate: false);
            }

            return await EnqueueLockedAsync(projectId, cancellationToken);
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
            if (_activeByProject.ContainsKey(projectId) || !_reservedProjects.Add(projectId))
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

    public bool TryGetProgress(Guid analysisId, out AnalysisProgressSnapshot? progress) =>
        _progress.TryGetValue(analysisId, out progress);

    public void Update(Guid analysisId, AnalysisPhase phase, string? message = null)
    {
        _progress[analysisId] = new AnalysisProgressSnapshot
        {
            AnalysisId = analysisId,
            Phase = phase,
            UpdatedAtUtc = _clock.UtcNow,
            Message = message,
        };
    }

    public async Task ReleaseAsync(Guid projectId)
    {
        await _gate.WaitAsync();
        try
        {
            _activeByProject.Remove(projectId);
        }
        finally
        {
            _gate.Release();
        }
    }

    public void Complete() => _channel.Writer.TryComplete();

    public void Forget(Guid analysisId) => _progress.TryRemove(analysisId, out _);

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
        Guid projectId,
        CancellationToken cancellationToken)
    {
        var analysisId = await StartAnalysisAsync(projectId, cancellationToken);
        if (analysisId is null)
        {
            return new AnalysisEnqueueResult(
                AnalysisEnqueueKind.ProjectNotFound,
                AnalysisId: null,
                IsDuplicate: false);
        }

        var workItem = PrepareWork(projectId, analysisId.Value);
        if (_channel.Writer.TryWrite(workItem))
        {
            return Accepted(analysisId.Value);
        }

        await FailQueueFullAsync(workItem);
        _activeByProject.Remove(projectId);
        Forget(analysisId.Value);
        return new AnalysisEnqueueResult(
            AnalysisEnqueueKind.QueueFull,
            analysisId,
            IsDuplicate: false);
    }

    private AnalysisWorkItem PrepareWork(Guid projectId, Guid analysisId)
    {
        _activeByProject[projectId] = analysisId;
        Update(analysisId, AnalysisPhase.Waiting);
        return new AnalysisWorkItem(analysisId, projectId);
    }

    private static AnalysisEnqueueResult Accepted(Guid analysisId) => new(
        AnalysisEnqueueKind.Accepted,
        analysisId,
        IsDuplicate: false);

    private async Task<Guid?> StartAnalysisAsync(
        Guid projectId,
        CancellationToken cancellationToken)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var repository = scope.ServiceProvider.GetRequiredService<IAnalysisRepository>();
        try
        {
            return await repository.StartAsync(projectId, _clock.UtcNow, cancellationToken);
        }
        catch (KeyNotFoundException)
        {
            return null;
        }
    }

    private async Task FailQueueFullAsync(AnalysisWorkItem item)
    {
        Update(item.AnalysisId, AnalysisPhase.Failed, "La file d’analyses est pleine.");
        await using var scope = _scopeFactory.CreateAsyncScope();
        var repository = scope.ServiceProvider.GetRequiredService<IAnalysisRepository>();
        var failure = new AnalysisFailure(
            "analysis.queue_full",
            "La file d’analyses est pleine.",
            _clock.UtcNow);
        await repository.FailAsync(item.AnalysisId, failure, CancellationToken.None);
    }
}
