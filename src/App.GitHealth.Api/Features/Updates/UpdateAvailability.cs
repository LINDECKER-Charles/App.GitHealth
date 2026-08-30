namespace App.GitHealth.Api.Features.Updates;

/// <summary>Ce que l'application peut dire d'une mise à jour, de son point de vue.</summary>
internal enum UpdateAvailability
{
    /// <summary>
    /// Aucune mise à jour in-app : Docker, navigateur, Linux, ou copie portable.
    /// </summary>
    Unsupported,

    /// <summary>La version installée est la dernière publiée.</summary>
    UpToDate,

    /// <summary>
    /// La source des releases est injoignable — hors ligne, quota atteint, dépôt
    /// indisponible. L'application ne peut ni proposer ni écarter une mise à jour.
    /// </summary>
    Unknown,

    /// <summary>Une version plus récente est publiée et installable.</summary>
    Available,
}
