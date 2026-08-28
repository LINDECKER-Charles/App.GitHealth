namespace App.GitHealth.Core.Analysis;

public interface IRepositoryScanner
{
    Task<RepositoryResult<RepositoryDescriptor>> InspectAsync(
        string repositoryPath,
        CancellationToken cancellationToken);

    Task<RepositoryResult<RepositoryScan>> ScanAsync(
        RepositoryScanRequest request,
        CancellationToken cancellationToken);

    Task<RepositoryResult<RepositoryScan>> ScanAsync(
        RepositoryScanRequest request,
        IProgress<RepositoryScanStage> progress,
        CancellationToken cancellationToken) => ScanAsync(request, cancellationToken);
}
