using Photino.NET;

namespace App.GitHealth.Api.Hosting.Desktop;

/// <summary>
/// Fenêtre de bureau adossée au moteur de rendu du système. À ouvrir depuis le thread
/// principal du processus : WebView2 ne s'initialise pas hors apartment STA, et macOS
/// impose son thread principal pour la boucle d'évènements.
/// </summary>
internal static class DesktopWindow
{
    private const string WindowTitle = "GitHealth";

    /// <summary>
    /// Taille de restauration, en pixels physiques. Elle ne garantit pas la largeur CSS :
    /// sur un écran à 150 %, 1360 pixels physiques ne font que 907 pixels CSS, sous le
    /// <c>min-width: 1180px</c> de l'espace de travail. D'où l'ouverture maximisée.
    /// </summary>
    private const int RestoredWidth = 1360;
    private const int RestoredHeight = 860;

    private const int MinimumWidth = 960;
    private const int MinimumHeight = 600;

    /// <summary>
    /// Photino journalise sur la console à partir de 1 : l'hôte reste seul à parler.
    /// </summary>
    private const int SilentLogVerbosity = 0;

    private const string IconFileName = "githealth.ico";

    /// <summary>
    /// Ouvre la fenêtre et rend la main à sa fermeture.
    /// </summary>
    /// <returns>
    /// <see langword="null" /> après une fermeture normale, sinon la raison pour laquelle
    /// aucune fenêtre n'a pu s'ouvrir — à l'appelant de basculer sur le navigateur.
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
            return "Le moteur de rendu du système est indisponible : "
                + $"{Describe(exception)} GitHealth bascule sur le navigateur.";
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
    /// Seul Windows consomme un <c>.ico</c> ; ailleurs l'icône vient de l'empaquetage.
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
    /// Photino réemballe toute panne d'initialisation native dans une
    /// <see cref="ApplicationException" /> et signale des paramètres de départ invalides
    /// par une <see cref="ArgumentException" />. Dans les deux cas, le navigateur reste
    /// une issue préférable à l'arrêt de l'application.
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
