using App.GitHealth.Api.Persistence.Entities;
using App.GitHealth.Api.Persistence.Models;
using App.GitHealth.Api.Persistence.Repositories;
using App.GitHealth.Core.Analysis;
using App.GitHealth.Core.Branches;

namespace App.GitHealth.Api.Features.Analyses;

internal sealed partial class AnalysisWorker(
    AnalysisQueue queue,
    IServiceScopeFactory scopeFactory,
    IRepositoryScanner scanner,
    ILogger<AnalysisWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await foreach (var item in queue.ReadAllAsync(stoppingToken))
            {
                await ProcessAsync(item, stoppingToken);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }
        finally
        {
            await CancelPendingAsync();
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        queue.Complete();
        await base.StopAsync(cancellationToken);
    }

    private async Task ProcessAsync(
        AnalysisWorkItem item,
        CancellationToken cancellationToken)
    {
        try
        {
            await RunPipelineAsync(item, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            await CancelAsync(item);
        }
        catch (Exception exception)
        {
            await FailUnexpectedAsync(item, exception);
        }
        finally
        {
            await queue.ReleaseAsync(item.ProjectId);
            queue.Forget(item.AnalysisId);
        }
    }

    private async Task RunPipelineAsync(
        AnalysisWorkItem item,
        CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var projects = scope.ServiceProvider.GetRequiredService<IProjectRepository>();
        var analyses = scope.ServiceProvider.GetRequiredService<IAnalysisRepository>();
        var project = await projects.GetAsync(item.ProjectId, cancellationToken)
            ?? throw new KeyNotFoundException("Le projet demandé n’existe pas.");
        var analysis = await analyses.GetAsync(item.AnalysisId, cancellationToken)
            ?? throw new KeyNotFoundException("L’analyse demandée n’existe pas.");
        var request = CreateRequest(project.RepositoryPath, analysis);
        var progress = new InlineScanProgress(stage => Report(item.AnalysisId, stage));
        var result = await scanner.ScanAsync(request, progress, cancellationToken);
        if (!result.TryGetValue(out var scan))
        {
            await FailScanAsync(item, analyses, result.Error!);
            await MarkUnavailableAsync(item.ProjectId, result.Error!, projects);
            return;
        }

        queue.Update(item.AnalysisId, AnalysisPhase.Persistence);
        var completion = new AnalysisCompletion(scan, queue.UtcNow);
        await analyses.CompleteAsync(item.AnalysisId, completion, cancellationToken);
        queue.Update(item.AnalysisId, AnalysisPhase.Finished);
    }

    private static RepositoryScanRequest CreateRequest(
        string repositoryPath,
        AnalysisRunEntity analysis) => new(
            repositoryPath,
            new GitRef(analysis.ReferenceName),
            analysis.BranchNamespace);

    private async Task FailScanAsync(
        AnalysisWorkItem item,
        IAnalysisRepository repository,
        RepositoryError error)
    {
        var code = $"git.{error.Code.ToString().ToLowerInvariant()}";
        var failure = new AnalysisFailure(code, error.Message, queue.UtcNow);
        await repository.FailAsync(item.AnalysisId, failure, CancellationToken.None);
        queue.Update(item.AnalysisId, AnalysisPhase.Failed, error.Message);
    }

    private async Task CancelAsync(AnalysisWorkItem item)
    {
        var failure = new AnalysisFailure(
            "analysis.cancelled",
            "L’analyse a été annulée pendant l’arrêt de l’application.",
            queue.UtcNow,
            IsCancellation: true);
        await TryFailAsync(item, failure);
        queue.Update(item.AnalysisId, AnalysisPhase.Cancelled, failure.Message);
    }

    private async Task FailUnexpectedAsync(AnalysisWorkItem item, Exception exception)
    {
        LogUnexpectedFailure(logger, item.AnalysisId, item.ProjectId, exception);
        var failure = new AnalysisFailure(
            "analysis.unexpected",
            "Une erreur inattendue a interrompu l’analyse.",
            queue.UtcNow);
        await TryFailAsync(item, failure);
        queue.Update(item.AnalysisId, AnalysisPhase.Failed, exception.Message);
    }

    private async Task TryFailAsync(AnalysisWorkItem item, AnalysisFailure failure)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var repository = scope.ServiceProvider.GetRequiredService<IAnalysisRepository>();
        var analysis = await repository.GetAsync(item.AnalysisId, CancellationToken.None);
        if (analysis?.Status == AnalysisRunStatus.Running)
        {
            await repository.FailAsync(item.AnalysisId, failure, CancellationToken.None);
        }
    }

    private async Task CancelPendingAsync()
    {
        while (queue.TryRead(out var item))
        {
            await CancelAsync(item!);
            await queue.ReleaseAsync(item!.ProjectId);
            queue.Forget(item.AnalysisId);
        }
    }

    private Task MarkUnavailableAsync(
        Guid projectId,
        RepositoryError error,
        IProjectRepository projects)
    {
        return error.Code is RepositoryErrorCode.PathNotFound
            or RepositoryErrorCode.NotARepository
            ? projects.MarkUnavailableAsync(projectId, queue.UtcNow, CancellationToken.None)
            : Task.CompletedTask;
    }

    private void Report(Guid analysisId, RepositoryScanStage stage)
    {
        var phase = stage == RepositoryScanStage.Topology
            ? AnalysisPhase.Topology
            : AnalysisPhase.Enrichment;
        queue.Update(analysisId, phase);
    }

    [LoggerMessage(
        EventId = 2001,
        Level = LogLevel.Error,
        Message = "L’analyse {AnalysisId} du projet {ProjectId} a échoué de façon inattendue.")]
    private static partial void LogUnexpectedFailure(
        ILogger logger,
        Guid analysisId,
        Guid projectId,
        Exception exception);
}
