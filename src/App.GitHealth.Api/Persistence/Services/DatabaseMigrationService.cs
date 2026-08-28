using Microsoft.EntityFrameworkCore;

namespace App.GitHealth.Api.Persistence.Services;

internal sealed class DatabaseMigrationService(
    IDbContextFactory<GitHealthDbContext> contextFactory,
    SqliteConnectionFactory connectionFactory)
    : IDatabaseMigrationService
{
    public async Task InitializeAsync(CancellationToken cancellationToken)
    {
        EnsureDatabaseDirectory();
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        await context.Database.MigrateAsync(cancellationToken);
        await ConfigureSqliteAsync(cancellationToken);
    }

    private void EnsureDatabaseDirectory()
    {
        var directory = Path.GetDirectoryName(connectionFactory.DatabasePath)
            ?? throw new InvalidOperationException("Le dossier SQLite est introuvable.");
        Directory.CreateDirectory(directory);
    }

    private async Task ConfigureSqliteAsync(CancellationToken cancellationToken)
    {
        await using var connection = connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await ExecutePragmaAsync(connection, "PRAGMA journal_mode=WAL;", cancellationToken);
        await ExecutePragmaAsync(connection, "PRAGMA foreign_keys=ON;", cancellationToken);
        var timeout = checked(connectionFactory.WriteTimeoutSeconds * 1000);
        await ExecutePragmaAsync(connection, $"PRAGMA busy_timeout={timeout};", cancellationToken);
    }

    private static async Task ExecutePragmaAsync(
        Microsoft.Data.Sqlite.SqliteConnection connection,
        string commandText,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = commandText;
        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}
