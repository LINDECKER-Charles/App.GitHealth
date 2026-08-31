namespace App.GitHealth.Api.Persistence.Services;

internal sealed class DatabaseInUseException : Exception
{
    public DatabaseInUseException(string databasePath, Exception innerException)
        : base(
            $"The SQLite database \"{databasePath}\" is already used by another GitHealth "
            + "instance. Close that instance and try again.",
            innerException)
    {
        DatabasePath = databasePath;
    }

    public string DatabasePath { get; }
}
