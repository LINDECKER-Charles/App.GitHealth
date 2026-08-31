using Photino.NET;

namespace App.GitHealth.Api.Hosting.Desktop;

/// <summary>
/// Desktop window backed by the system rendering engine. To be opened from the process
/// main thread: WebView2 does not initialise outside the STA apartment, and macOS
/// requires its main thread for the event loop.
/// </summary>
internal static class DesktopWindow
{
    private const string WindowTitle = "GitHealth";

    /// <summary>
    /// Restored size, in physical pixels. It does not guarantee the CSS width: on a
    /// screen at 150%, 1360 physical pixels are only 907 CSS pixels, below the
    /// <c>min-width: 1180px</c> of the workspace. Hence opening maximised.
    /// </summary>
    private const int RestoredWidth = 1360;
    private const int RestoredHeight = 860;

    private const int MinimumWidth = 960;
    private const int MinimumHeight = 600;

    /// <summary>
    /// Photino logs to the console from 1 upwards: the host stays the only voice.
    /// </summary>
    private const int SilentLogVerbosity = 0;

    private const string IconFileName = "githealth.ico";

    /// <summary>
    /// Opens the window and returns when it closes.
    /// </summary>
    /// <returns>
    /// <see langword="null" /> after a normal close, otherwise the reason why no
    /// window could be opened — the caller then falls back to the browser.
    /// </returns>
    public static string? Open(Uri address)
    {
        ArgumentNullException.ThrowIfNull(address);
        try
        {
            Create(address).WaitForClose();
            return null;
        }
        catch (Exception exception) when (IsEngineUnavailable(exception))
        {
            return "The system rendering engine is unavailable: "
                + $"{Describe(exception)} GitHealth falls back to the browser.";
        }
    }

    private static PhotinoWindow Create(Uri address)
    {
        var window = new PhotinoWindow()
            .SetLogVerbosity(SilentLogVerbosity)
            .SetTitle(WindowTitle)
            .SetUseOsDefaultSize(false)
            .SetSize(RestoredWidth, RestoredHeight)
            .SetMinSize(MinimumWidth, MinimumHeight)
            .SetMaximized(true)
            .SetContextMenuEnabled(false)
            .SetDevToolsEnabled(false);
        var iconPath = ResolveWindowsIconPath();
        if (iconPath is not null)
        {
            window = window.SetIconFile(iconPath);
        }

        return DesktopFolderBridge.Register(window).Load(address);
    }

    /// <summary>
    /// Only Windows consumes a <c>.ico</c>; elsewhere the icon comes from the packaging.
    /// </summary>
    private static string? ResolveWindowsIconPath()
    {
        if (!OperatingSystem.IsWindows())
        {
            return null;
        }

        var iconPath = Path.Combine(AppContext.BaseDirectory, IconFileName);
        return File.Exists(iconPath) ? iconPath : null;
    }

    /// <summary>
    /// Photino rewraps every native initialisation failure into an
    /// <see cref="ApplicationException" /> and reports invalid start-up parameters
    /// with an <see cref="ArgumentException" />. In both cases, the browser stays a
    /// better way out than shutting the application down.
    /// </summary>
    private static bool IsEngineUnavailable(Exception exception) => exception switch
    {
        ApplicationException { InnerException: { } inner } => IsNativeLoadFailure(inner),
        ArgumentException => true,
        _ => IsNativeLoadFailure(exception),
    };

    private static bool IsNativeLoadFailure(Exception exception) => exception
        is DllNotFoundException
        or EntryPointNotFoundException
        or TypeInitializationException
        or BadImageFormatException;

    private static string Describe(Exception exception) =>
        (exception.InnerException ?? exception).Message;
}
