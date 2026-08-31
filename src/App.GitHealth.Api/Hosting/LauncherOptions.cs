using System.Net;

namespace App.GitHealth.Api.Hosting;

internal sealed record LauncherOptions
{
    public const int AutomaticPort = 0;

    public string? RepositoryPath { get; init; }

    public int Port { get; init; } = AutomaticPort;

    public string? DataDirectory { get; init; }

    /// <summary>
    /// Explicit path to the Git executable, when it is not on the <c>PATH</c>.
    /// </summary>
    public string? GitExecutablePath { get; init; }

    /// <summary>
    /// False on <c>--no-browser</c>, which means "no interface" and therefore also
    /// covers the window. The interpretation belongs to <c>DesktopDisplayModeResolver</c>.
    /// </summary>
    public bool ShouldOpenBrowser { get; init; } = true;

    /// <summary>
    /// False on <c>--no-window</c>: the interface goes back through the system browser.
    /// </summary>
    public bool ShouldOpenWindow { get; init; } = true;

    public bool ShowHelp { get; init; }

    public IReadOnlyList<string> HostArguments { get; init; } = [];

    public static IPAddress ListenAddress => IPAddress.Loopback;

    public static Uri CreateApplicationAddress(int boundPort)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(boundPort, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(boundPort, ushort.MaxValue);
        return new UriBuilder(Uri.UriSchemeHttp, ListenAddress.ToString(), boundPort).Uri;
    }
}
