namespace App.GitHealth.Api.Persistence.Services;

internal sealed class DatabaseInUseException : Exception
{
    public DatabaseInUseException(string databasePath, Exception innerException)
        : base(
            $"La base SQLite « {databasePath} » est déjà utilisée par une autre instance "
            + "de GitHealth. Fermez cette instance avant de réessayer.",
            innerException)
    {
        DatabasePath = databasePath;
    }

    public string DatabasePath { get; }
}
