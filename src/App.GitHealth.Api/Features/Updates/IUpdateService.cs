namespace App.GitHealth.Api.Features.Updates;

/// <summary>
/// Source of truth for updates. The implementation depends on the run mode and on the
/// platform; the application itself knows only this contract.
/// </summary>
internal interface IUpdateService
{
    Task<UpdateStatus> GetStatusAsync(CancellationToken cancellationToken);

    /// <summary>Downloads the available update.</summary>
    /// <returns>True when an update is ready to be applied.</returns>
    Task<bool> DownloadAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Applies the downloaded update and restarts the application. This call never
    /// returns: it must follow the emission of the HTTP response, not precede it.
    /// </summary>
    void ApplyAndRestart();
}
