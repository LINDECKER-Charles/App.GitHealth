namespace App.GitHealth.Api.Hosting.Desktop;

/// <summary>
/// Traduit les options du lanceur en interface à ouvrir. <c>--no-browser</c> vaut
/// « aucune interface » et implique <c>--no-window</c> : la CI lance le binaire avec ce
/// drapeau et une fenêtre y resterait bloquée sur l'attente de fermeture.
/// </summary>
internal static class DesktopDisplayModeResolver
{
    public static DesktopDisplayMode Resolve(LauncherOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (!options.ShouldOpenBrowser)
        {
            return DesktopDisplayMode.None;
        }

        return options.ShouldOpenWindow
            ? DesktopDisplayMode.Window
            : DesktopDisplayMode.SystemBrowser;
    }
}
