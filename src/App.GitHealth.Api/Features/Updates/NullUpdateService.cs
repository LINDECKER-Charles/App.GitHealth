namespace App.GitHealth.Api.Features.Updates;

/// <summary>
/// Default implementation: no in-app update. It serves in Docker, in browser mode and
/// on Linux, where the user expects their package manager.
/// </summary>
internal sealed class NullUpdateService : IUpdateService
{
    public Task<UpdateStatus> GetStatusAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(UpdateStatus.Unsupported);
    }

    public Task<bool> DownloadAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(false);
    }

    public void ApplyAndRestart() => throw new NotSupportedException(
        "No in-app update is supported in this run mode.");
}
