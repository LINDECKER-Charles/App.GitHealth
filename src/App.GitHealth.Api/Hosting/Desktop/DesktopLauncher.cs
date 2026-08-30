using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;

namespace App.GitHealth.Api.Hosting.Desktop;

/// <summary>
/// Démarre l'hôte loopback puis ouvre l'interface demandée.
/// </summary>
/// <remarks>
/// Tout reste sur le thread appelant, qui est le thread principal du processus : la
/// fenêtre exige l'apartment STA sur Windows et la boucle principale sur macOS. Un
/// <c>await</c> reprendrait sur un thread du pool, d'où l'attente explicite des tâches
/// de l'hôte plutôt qu'une méthode asynchrone.
/// </remarks>
internal static class DesktopLauncher
{
    public static void Run(WebApplication app, LauncherOptions options)
    {
        ArgumentNullException.ThrowIfNull(app);
        ArgumentNullException.ThrowIfNull(options);
        app.StartAsync().GetAwaiter().GetResult();
        var address = LauncherOptions.CreateApplicationAddress(BoundPort(app));
        Console.WriteLine($"GitHealth est disponible sur {address}");
        if (OpenInterface(DesktopDisplayModeResolver.Resolve(options), address))
        {
            app.StopAsync().GetAwaiter().GetResult();
            return;
        }

        app.WaitForShutdownAsync().GetAwaiter().GetResult();
    }

    /// <returns>Vrai quand une fenêtre a tenu la session et vient d'être fermée.</returns>
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
            ?? throw new InvalidOperationException("Le port attribué est introuvable.");
    }
}
