namespace App.GitHealth.Api.Hosting.Desktop;

/// <summary>
/// Demande envoyée par la page à l'hôte. Le contenu vient de la webview : il est traité
/// comme une entrée non fiable, jamais comme une commande.
/// </summary>
internal sealed record DesktopBridgeRequest
{
    /// <summary>Corrèle la réponse à la demande ; le pont est asynchrone.</summary>
    public string? Id { get; init; }

    public string? Kind { get; init; }
}
