namespace App.GitHealth.Api.Hosting;

internal static class StartupFailureReporter
{
    public const int FailureExitCode = 1;

    public static string HelpText => """
        Usage : githealth [options]

        Options :
          --repo <chemin>       Préremplir le dépôt à analyser.
          --port <1-65535>      Utiliser un port loopback précis (automatique par défaut).
          --data-dir <chemin>   Choisir le répertoire des données locales.
          --git-path <chemin>   Utiliser cet exécutable Git plutôt que celui du PATH.
          --no-window           Ouvrir le navigateur système plutôt qu'une fenêtre.
          --no-browser          N'ouvrir aucune interface au démarrage.
          --help, -h            Afficher cette aide.
        """;

    public static string InvalidArguments(string details) =>
        $"Arguments invalides : {details} Utilisez --help pour afficher l’aide.";

    public static string PortUnavailable(int port) => port == LauncherOptions.AutomaticPort
        ? "Aucun port loopback disponible n’a pu être attribué à GitHealth."
        : $"Le port loopback {port} est déjà utilisé. Choisissez un autre port avec --port.";

    public static string KestrelEndpointsNotAllowed() =>
        "La configuration Kestrel:Endpoints est refusée par le lanceur natif afin de préserver "
        + "l’écoute loopback. Utilisez --port pour choisir le port local.";

    public static string DataDirectoryUnavailable(string directoryPath) =>
        $"Le répertoire de données « {directoryPath} » est inaccessible ou non inscriptible.";

    public static string DatabaseUnavailable(string databasePath) =>
        $"La base SQLite « {databasePath} » est invalide ou inaccessible. "
        + "Vérifiez le répertoire de données et ses droits.";

    public static string DatabaseInUse(string databasePath) =>
        $"La base SQLite « {databasePath} » est déjà utilisée "
        + "par une autre instance de GitHealth.";

    public static string GitUnavailable(string? details = null)
    {
        var suffix = string.IsNullOrWhiteSpace(details) ? string.Empty : $" {details}";
        return "Git est introuvable ou ne peut pas être démarré. "
            + $"Installez Git puis relancez GitHealth.{suffix}";
    }

    public static string Unexpected() =>
        "GitHealth n’a pas pu démarrer. Consultez les journaux pour identifier la cause.";

    public static void Write(TextWriter errorOutput, string message)
    {
        ArgumentNullException.ThrowIfNull(errorOutput);
        ArgumentException.ThrowIfNullOrWhiteSpace(message);
        errorOutput.WriteLine(message);
    }
}
