namespace App.GitHealth.Api.Hosting.Desktop.Bridge;

/// <summary>
/// Hands an address to the system browser.
/// </summary>
/// <remarks>
/// A <c>target="_blank"</c> dies in silence inside the window: Photino implements no
/// <c>createWebViewWithConfiguration</c> delegate on macOS, and its native layer holds no
/// reference to <c>NSWorkspace</c> at all, so WebKit has nobody to ask for a new window.
/// The page therefore asks the host, which owns a process.
/// </remarks>
internal static class DesktopExternalLink
{
    public const string Kind = "openExternal";

    public static DesktopBridgeReply Handle(string? url, string id) => new()
    {
        Id = id,
        Kind = Kind,
        IsHandled = Open(url, new SystemBrowserLauncher()),
    };

    /// <summary>
    /// The address comes from the page: only an absolute one is considered, and
    /// <see cref="SystemBrowserLauncher" /> turns away everything that is not http(s).
    /// </summary>
    internal static bool Open(string? url, SystemBrowserLauncher launcher)
    {
        ArgumentNullException.ThrowIfNull(launcher);
        if (!Uri.TryCreate(url, UriKind.Absolute, out var address))
        {
            return false;
        }

        var warning = launcher.Open(address);
        if (warning is null)
        {
            return true;
        }

        Console.Error.WriteLine(warning);
        return false;
    }
}
