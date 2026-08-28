using App.GitHealth.Api.Persistence.Entities;
using App.GitHealth.Api.Persistence.Models;

namespace App.GitHealth.Api.Persistence.Repositories;

internal interface IAnalysisRepository
{
    Task<Guid> StartAsync(
        Guid projectId,
        DateTimeOffset startedAtUtc,
        CancellationToken cancellationToken);

    Task CompleteAsync(
        Guid analysisId,
        AnalysisCompletion completion,
        CancellationToken cancellationToken);

    Task FailAsync(
        Guid analysisId,
        AnalysisFailure failure,
        CancellationToken cancellationToken);

    Task<AnalysisRunEntity?> GetAsync(Guid analysisId, CancellationToken cancellationToken);

    Task<AnalysisRunEntity?> GetLastSuccessfulAsync(
        Guid projectId,
        CancellationToken cancellationToken);

    Task<AnalysisHistoryPage> GetHistoryAsync(
        Guid projectId,
        AnalysisHistoryRange range,
        CancellationToken cancellationToken);

    Task<BranchSnapshotEntity?> GetBranchAsync(
        Guid branchSnapshotId,
        CancellationToken cancellationToken);
}
