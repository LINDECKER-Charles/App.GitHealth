namespace App.GitHealth.Api.Hosting.Desktop;

/// <summary>Interface opened at startup in native mode.</summary>
internal enum DesktopDisplayMode
{
    /// <summary>
    /// No interface: the host runs on its own, for tests and automation.
    /// </summary>
    None,

    /// <summary>Desktop window embedding the system webview.</summary>
    Window,

    /// <summary>System browser on the loopback address.</summary>
    SystemBrowser,
}
