namespace App.GitHealth.Api.Features.Updates;

/// <summary>
/// Source de vérité des mises à jour. L'implémentation dépend du mode d'exécution et de
/// la plateforme ; l'application, elle, ne connaît que ce contrat.
/// </summary>
internal interface IUpdateService
{
    Task<UpdateStatus> GetStatusAsync(CancellationToken cancellationToken);

    /// <summary>Télécharge la mise à jour disponible.</summary>
    /// <returns>Vrai lorsqu'une mise à jour est prête à être appliquée.</returns>
    Task<bool> DownloadAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Applique la mise à jour téléchargée et relance l'application. Cet appel ne rend
    /// jamais la main : il doit suivre l'émission de la réponse HTTP, pas la précéder.
    /// </summary>
    void ApplyAndRestart();
}
