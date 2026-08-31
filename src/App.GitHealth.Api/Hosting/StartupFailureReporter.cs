using App.GitHealth.Api.Persistence.Services;
using Microsoft.AspNetCore.Connections;
using Microsoft.Data.Sqlite;

namespace App.GitHealth.Api.Hosting;

internal static class StartupFailureReporter
{
    public const int FailureExitCode = 1;

    public static string HelpText => """
        Usage: githealth [options]

        Options:
          --repo <path>         Pre-fill the repository offered on the home screen.
          --port <1-65535>      Force a specific port on the loopback interface.
          --data-dir <path>     Move the database and its instance lock.
          --git-path <path>     Force the Git executable to use.
          --no-window           Open the interface in the system browser.
          --no-browser          Open no interface at startup.
          --help, -h            Print the help and exit.
        """;

    public static string InvalidArguments(string details) =>
        $"Invalid arguments: {details} Use --help to print the help.";

    public static string PortUnavailable(int port) => port == LauncherOptions.AutomaticPort
        ? "No loopback port could be assigned to GitHealth."
        : $"Loopback port {port} is already in use. Choose another port with --port.";

    public static string KestrelEndpointsNotAllowed() =>
        "The Kestrel:Endpoints configuration is refused by the native launcher to "
        + "preserve loopback-only listening. Use --port to choose the local port.";

    public static string DataDirectoryUnavailable(string directoryPath) =>
        $"The data directory \"{directoryPath}\" is unreachable or not writable.";

    public static string DatabaseUnavailable(string databasePath) =>
        $"The SQLite database \"{databasePath}\" is invalid or unreachable. "
        + "Check the data directory and its permissions.";

    public static string DatabaseInUse(string databasePath) =>
        $"The SQLite database \"{databasePath}\" is already used "
        + "by another GitHealth instance.";

    public static string GitUnavailable(string? details = null)
    {
        var suffix = string.IsNullOrWhiteSpace(details) ? string.Empty : $" {details}";
        return "Git cannot be found or started. "
            + $"Install Git, then start GitHealth again.{suffix}";
    }

    public static string Unexpected() =>
        "GitHealth could not start. Check the logs to identify the cause.";

    /// <summary>
    /// Turns a startup failure into an actionable message. The causes often hide in
    /// an inner exception: the search walks the whole chain.
    /// </summary>
    public static string Diagnose(Exception exception, int port, string databasePath)
    {
        ArgumentNullException.ThrowIfNull(exception);
        if (Find<DatabaseInUseException>(exception) is { } inUse)
        {
            return DatabaseInUse(inUse.DatabasePath);
        }

        if (Find<AddressInUseException>(exception) is not null)
        {
            return PortUnavailable(port);
        }

        if (Find<SqliteException>(exception) is not null)
        {
            return DatabaseUnavailable(databasePath);
        }

        var isDirectoryFailure = Find<UnauthorizedAccessException>(exception) is not null
            || Find<IOException>(exception) is not null;
        return isDirectoryFailure
            ? DataDirectoryUnavailable(Path.GetDirectoryName(databasePath) ?? databasePath)
            : Unexpected();
    }

    private static TException? Find<TException>(Exception exception)
        where TException : Exception
    {
        for (var current = exception; current is not null; current = current.InnerException)
        {
            if (current is TException match)
            {
                return match;
            }
        }

        return null;
    }

    public static void Write(TextWriter errorOutput, string message)
    {
        ArgumentNullException.ThrowIfNull(errorOutput);
        ArgumentException.ThrowIfNullOrWhiteSpace(message);
        errorOutput.WriteLine(message);
    }
}
