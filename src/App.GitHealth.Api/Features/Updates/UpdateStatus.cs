namespace App.GitHealth.Api.Features.Updates;

/// <summary>État des mises à jour, tel que servi par <c>GET /api/updates</c>.</summary>
internal sealed record UpdateStatus
{
    /// <summary>
    /// Nom d'un <see cref="UpdateAvailability" />. Comme les autres réponses de l'API,
    /// le contrat reste textuel plutôt que numérique.
    /// </summary>
    public required string Availability { get; init; }

    /// <summary>Version installée, ou <see langword="null" /> hors installation gérée.</summary>
    public string? CurrentVersion { get; init; }

    /// <summary>Version publiée plus récente, renseignée seulement si elle existe.</summary>
    public string? AvailableVersion { get; init; }

    public static UpdateStatus Unsupported { get; } = For(UpdateAvailability.Unsupported);

    public static UpdateStatus For(
        UpdateAvailability availability,
        string? currentVersion = null,
        string? availableVersion = null)
    {
        return new UpdateStatus
        {
            Availability = availability.ToString(),
            CurrentVersion = currentVersion,
            AvailableVersion = availableVersion,
        };
    }
}
