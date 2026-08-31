using Microsoft.Data.Sqlite;

namespace App.GitHealth.Api.Persistence.Services;

internal sealed class SqliteDatabaseBackupService(SqliteConnectionFactory connectionFactory)
    : IDatabaseBackupService
{
    private const int CopyBufferSize = 81920;

    public async Task ExportAsync(Stream destination, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(destination);
        if (!destination.CanWrite)
        {
            throw new ArgumentException(
                "The backup stream must be writable.",
                nameof(destination));
        }

        var backupPath = CreateTemporaryPath();
        try
        {
            await CreateBackupAsync(backupPath, cancellationToken);
            await CopyBackupAsync(backupPath, destination, cancellationToken);
        }
        finally
        {
            DeleteTemporaryDatabase(backupPath);
        }
    }

    private async Task CreateBackupAsync(string path, CancellationToken cancellationToken)
    {
        PrivateFilePermissions.CreateFile(path);
        await using var source = connectionFactory.CreateConnection();
        await using var target = new SqliteConnection(BuildBackupConnectionString(path));
        await source.OpenAsync(cancellationToken);
        await target.OpenAsync(cancellationToken);
        PrivateFilePermissions.EnsureFile(path);
        cancellationToken.ThrowIfCancellationRequested();
        source.BackupDatabase(target);
        await NormalizeJournalAsync(target, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
    }

    private static async Task NormalizeJournalAsync(
        SqliteConnection connection,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = "PRAGMA journal_mode=DELETE;";
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task CopyBackupAsync(
        string path,
        Stream destination,
        CancellationToken cancellationToken)
    {
        await using var source = new FileStream(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            CopyBufferSize,
            FileOptions.Asynchronous | FileOptions.SequentialScan);
        await source.CopyToAsync(destination, cancellationToken);
    }

    private static string CreateTemporaryPath()
    {
        var directory = PrivateFilePermissions.CreateTemporaryDirectory(
            "githealth-backup-");
        return Path.Combine(directory, "backup.db");
    }

    private static string BuildBackupConnectionString(string path)
    {
        return new SqliteConnectionStringBuilder
        {
            DataSource = path,
            Mode = SqliteOpenMode.ReadWrite,
            ForeignKeys = true,
            Pooling = false,
        }.ToString();
    }

    private static void DeleteTemporaryDatabase(string path)
    {
        File.Delete(path);
        File.Delete(path + "-wal");
        File.Delete(path + "-shm");
        var directory = Path.GetDirectoryName(path);
        if (directory is not null)
        {
            Directory.Delete(directory);
        }
    }
}
