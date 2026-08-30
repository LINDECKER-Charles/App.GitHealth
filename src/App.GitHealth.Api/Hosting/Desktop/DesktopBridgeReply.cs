namespace App.GitHealth.Api.Hosting.Desktop;

/// <summary>Réponse renvoyée à la page, portant l'identifiant de la demande d'origine.</summary>
internal sealed record DesktopBridgeReply
{
    public required string Id { get; init; }

    public required string Kind { get; init; }

    /// <summary>Chemin retenu, ou <see langword="null" /> quand l'utilisateur a annulé.</summary>
    public string? Path { get; init; }
}
