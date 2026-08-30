using System.Net;

namespace App.GitHealth.Api.Hosting;

internal sealed record LauncherOptions
{
    public const int AutomaticPort = 0;

    public string? RepositoryPath { get; init; }

    public int Port { get; init; } = AutomaticPort;

    public string? DataDirectory { get; init; }

    /// <summary>
    /// Chemin explicite de l'exécutable Git, quand il n'est pas sur le <c>PATH</c>.
    /// </summary>
    public string? GitExecutablePath { get; init; }

    /// <summary>
    /// Faux sur <c>--no-browser</c>, qui vaut « aucune interface » et couvre donc aussi
    /// la fenêtre. L'interprétation appartient à <c>DesktopDisplayModeResolver</c>.
    /// </summary>
    public bool ShouldOpenBrowser { get; init; } = true;

    /// <summary>
    /// Faux sur <c>--no-window</c> : l'interface repasse par le navigateur système.
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
