namespace App.GitHealth.Api.Hosting.Desktop;

/// <summary>Interface ouverte au démarrage en mode natif.</summary>
internal enum DesktopDisplayMode
{
    /// <summary>
    /// Aucune interface : l'hôte tourne seul, pour les tests et l'automatisation.
    /// </summary>
    None,

    /// <summary>Fenêtre de bureau embarquant la webview du système.</summary>
    Window,

    /// <summary>Navigateur système sur l'adresse loopback.</summary>
    SystemBrowser,
}
