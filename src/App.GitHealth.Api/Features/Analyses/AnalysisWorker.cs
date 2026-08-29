using App.GitHealth.Api.Features.Common;
using App.GitHealth.Api.Features.Projects;
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
    private const string UnexpectedFailureMessage =
        "L’analyse {AnalysisId} du projet {ProjectId} a échoué de façon inattendue.";

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var loops = Enumerable
            .Range(0, queue.MaximumParallelAnalyses)
            .Select(_ => ConsumeAsync(stoppingToken))
            .ToArray();
        try
        {
            await Task.WhenAll(loops);
        }
        finally
        {
            await CancelPendingAsync();
        }
    }

    /// <summary>
    /// Une boucle de lecture par analyse menée de front : la file répartit les projets entre
    /// elles, et un seul projet reste actif à la fois grâce à la réservation de la file.
    /// </summary>
    private async Task ConsumeAsync(CancellationToken stoppingToken)
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
        using var timeout = new CancellationTokenSource(queue.Timeout);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken,
            timeout.Token);
        try
        {
            await RunPipelineAsync(item, linked.Token);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            await CancelAsync(item);
        }
        catch (OperationCanceledException) when (timeout.IsCancellationRequested)
        {
            await FailTimedOutAsync(item);
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
        var execution = await LoadExecutionAsync(scope.ServiceProvider, item, cancellationToken);
        var request = await CreateValidatedRequestAsync(item, execution, cancellationToken);
        if (request is null)
        {
            return;
        }

        var progress = new InlineScanProgress(stage => Report(item.AnalysisId, stage));
        var result = await scanner.ScanAsync(request, progress, cancellationToken);
        if (!result.TryGetValue(out var scan))
        {
            await FailScanAsync(item, execution.Analyses, result.Error!);
            await MarkUnavailableAsync(item.ProjectId, result.Error!, execution.Projects);
            return;
        }

        queue.Update(item.AnalysisId, AnalysisPhase.Persistence);
        var completion = new AnalysisCompletion(scan, queue.UtcNow);
        await execution.Analyses.CompleteAsync(item.AnalysisId, completion, cancellationToken);
        queue.Update(item.AnalysisId, AnalysisPhase.Finished);
    }

    private static async Task<AnalysisExecution> LoadExecutionAsync(
        IServiceProvider services,
        AnalysisWorkItem item,
        CancellationToken cancellationToken)
    {
        var projects = services.GetRequiredService<IProjectRepository>();
        var analyses = services.GetRequiredService<IAnalysisRepository>();
        var project = await projects.GetAsync(item.ProjectId, cancellationToken)
            ?? throw new KeyNotFoundException("Le projet demandé n’existe pas.");
        var analysis = await analyses.GetAsync(item.AnalysisId, cancellationToken)
            ?? throw new KeyNotFoundException("L’analyse demandée n’existe pas.");
        return new AnalysisExecution(services, project, analysis);
    }

    private async Task<RepositoryScanRequest?> CreateValidatedRequestAsync(
        AnalysisWorkItem item,
        AnalysisExecution execution,
        CancellationToken cancellationToken)
    {
        var validator = execution.Services.GetRequiredService<RepositoryValidator>();
        var validation = await validator.ValidateAsync(
            execution.Project.RepositoryPath,
            cancellationToken);
        if (validation.IsSuccess)
        {
            return CreateRequest(
                validation.Value!.Location.CanonicalPath,
                execution.Analysis);
        }

        await FailValidationAsync(item, validation.Failure!, execution);
        return null;
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

    private async Task FailTimedOutAsync(AnalysisWorkItem item)
    {
        var failure = new AnalysisFailure(
            "analysis.timed_out",
            "L’analyse a dépassé le délai global autorisé.",
            queue.UtcNow);
        await TryFailAsync(item, failure);
        queue.Update(item.AnalysisId, AnalysisPhase.Failed, failure.Message);
    }

    private async Task FailValidationAsync(
        AnalysisWorkItem item,
        ApiFailure failure,
        AnalysisExecution execution)
    {
        var analysisFailure = new AnalysisFailure(
            failure.Code,
            failure.Detail,
            queue.UtcNow);
        await execution.Analyses.FailAsync(
            item.AnalysisId,
            analysisFailure,
            CancellationToken.None);
        if (failure.Code is ApiErrorCodes.InvalidPath
            or ApiErrorCodes.InvalidRepository
            or ApiErrorCodes.PathNotAllowed)
        {
            await execution.Projects.MarkUnavailableAsync(
                item.ProjectId,
                queue.UtcNow,
                CancellationToken.None);
        }

        queue.Update(item.AnalysisId, AnalysisPhase.Failed, failure.Detail);
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
        Message = UnexpectedFailureMessage)]
    private static partial void LogUnexpectedFailure(
        ILogger logger,
        Guid analysisId,
        Guid projectId,
        Exception exception);

    private sealed record AnalysisExecution(
        IServiceProvider Services,
        ProjectEntity Project,
        AnalysisRunEntity Analysis)
    {
        public IProjectRepository Projects =>
            Services.GetRequiredService<IProjectRepository>();

        public IAnalysisRepository Analyses =>
            Services.GetRequiredService<IAnalysisRepository>();
    }
}
