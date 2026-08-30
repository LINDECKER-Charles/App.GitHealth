namespace App.GitHealth.Api.Features.Updates;

/// <summary>
/// Implémentation par défaut : aucune mise à jour in-app. Elle sert en Docker, en mode
/// navigateur et sur Linux, où l'utilisateur attend son gestionnaire de paquets.
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
        "Aucune mise à jour in-app n'est prise en charge dans ce mode d'exécution.");
}
