using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;

namespace App.GitHealth.Api.Hosting.Desktop;

/// <summary>
/// Starts the loopback host, then opens the requested interface.
/// </summary>
/// <remarks>
/// Everything stays on the calling thread, which is the process main thread: the
/// window requires the STA apartment on Windows and the main loop on macOS. An
/// <c>await</c> would resume on a pool thread, hence the explicit wait on the host
/// tasks rather than an asynchronous method.
/// </remarks>
internal static class DesktopLauncher
{
    public static void Run(WebApplication app, LauncherOptions options)
    {
        ArgumentNullException.ThrowIfNull(app);
        ArgumentNullException.ThrowIfNull(options);
        app.StartAsync().GetAwaiter().GetResult();
        var address = LauncherOptions.CreateApplicationAddress(BoundPort(app));
        Console.WriteLine($"GitHealth is available at {address}");
        if (OpenInterface(DesktopDisplayModeResolver.Resolve(options), address))
        {
            app.StopAsync().GetAwaiter().GetResult();
            return;
        }

        app.WaitForShutdownAsync().GetAwaiter().GetResult();
    }

    /// <returns>True when a window held the session and has just been closed.</returns>
    private static bool OpenInterface(DesktopDisplayMode mode, Uri address)
    {
        if (mode == DesktopDisplayMode.None)
        {
            return false;
        }

        if (mode == DesktopDisplayMode.Window && OpenWindow(address))
        {
            return true;
        }

        OpenSystemBrowser(address);
        return false;
    }

    private static bool OpenWindow(Uri address)
    {
        var failure = DesktopWindow.Open(address);
        if (failure is null)
        {
            return true;
        }

        Console.Error.WriteLine(failure);
        return false;
    }

    private static void OpenSystemBrowser(Uri address)
    {
        var warning = new SystemBrowserLauncher().Open(address);
        if (warning is not null)
        {
            Console.Error.WriteLine(warning);
        }
    }

    private static int BoundPort(WebApplication app)
    {
        var server = app.Services.GetRequiredService<IServer>();
        var addresses = server.Features.Get<IServerAddressesFeature>()?.Addresses;
        var address = addresses?.Select(value => new Uri(value)).SingleOrDefault();
        return address?.Port
            ?? throw new InvalidOperationException("The assigned port cannot be found.");
    }
}
