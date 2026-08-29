using App.GitHealth.Api.Persistence.Models;
using App.GitHealth.Core.Common;
using Microsoft.EntityFrameworkCore;

namespace App.GitHealth.Api.Persistence.Services;

internal sealed class DatabaseMigrationService(
    IDbContextFactory<GitHealthDbContext> contextFactory,
    SqliteConnectionFactory connectionFactory,
    IClock clock)
    : IDatabaseMigrationService
{
    public async Task InitializeAsync(CancellationToken cancellationToken)
    {
        EnsureDatabaseDirectory();
        EnsureDatabaseFile();
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        await context.Database.MigrateAsync(cancellationToken);
        await RecoverInterruptedAnalysesAsync(context, cancellationToken);
        await ConfigureSqliteAsync(cancellationToken);
        EnsureDatabaseFilePermissions();
    }

    private async Task RecoverInterruptedAnalysesAsync(
        GitHealthDbContext context,
        CancellationToken cancellationToken)
    {
        var interrupted = await context.AnalysisRuns
            .Where(analysis => analysis.Status == AnalysisRunStatus.Running)
            .ToListAsync(cancellationToken);
        var failure = new AnalysisFailure(
            "analysis.interrupted",
            "L’analyse a été interrompue par l’arrêt de l’application.",
            clock.UtcNow,
            IsCancellation: true);
        foreach (var analysis in interrupted)
        {
            analysis.Fail(failure);
        }

        await context.SaveChangesAsync(cancellationToken);
    }

    private void EnsureDatabaseDirectory()
    {
        var directory = Path.GetDirectoryName(connectionFactory.DatabasePath)
            ?? throw new InvalidOperationException("Le dossier SQLite est introuvable.");
        PrivateFilePermissions.EnsureDirectory(directory);
    }

    private void EnsureDatabaseFile()
    {
        if (!File.Exists(connectionFactory.DatabasePath))
        {
            PrivateFilePermissions.CreateFile(connectionFactory.DatabasePath);
        }

        PrivateFilePermissions.EnsureFile(connectionFactory.DatabasePath);
    }

    private void EnsureDatabaseFilePermissions()
    {
        foreach (var path in SqliteFiles())
        {
            if (File.Exists(path))
            {
                PrivateFilePermissions.EnsureFile(path);
            }
        }
    }

    private IEnumerable<string> SqliteFiles()
    {
        yield return connectionFactory.DatabasePath;
        yield return connectionFactory.DatabasePath + "-wal";
        yield return connectionFactory.DatabasePath + "-shm";
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
