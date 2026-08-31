namespace App.GitHealth.Api.Hosting.Desktop;

/// <summary>
/// Turns the launcher options into the interface to open. <c>--no-browser</c> means
/// "no interface" and implies <c>--no-window</c>: the CI runs the binary with that
/// flag and a window would stay blocked there waiting to be closed.
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
