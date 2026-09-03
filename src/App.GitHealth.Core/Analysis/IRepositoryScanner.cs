using App.GitHealth.Core.Branches;

namespace App.GitHealth.Core.Analysis;

public interface IRepositoryScanner
{
    Task<RepositoryResult<RepositoryDescriptor>> InspectAsync(
        string repositoryPath,
        CancellationToken cancellationToken);

    Task<RepositoryResult<bool>> ContainsCommitAsync(
        string repositoryPath,
        CommitId commit,
        CancellationToken cancellationToken);

    Task<RepositoryResult<RepositoryScan>> ScanAsync(
        RepositoryScanRequest request,
        CancellationToken cancellationToken);

    Task<RepositoryResult<RepositoryScan>> ScanAsync(
        RepositoryScanRequest request,
        IProgress<RepositoryScanEvent> progress,
        CancellationToken cancellationToken) => ScanAsync(request, cancellationToken);
}
