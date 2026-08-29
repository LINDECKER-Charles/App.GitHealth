using App.GitHealth.Api.Persistence.Repositories;
using App.GitHealth.Api.Persistence.Services;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;

namespace App.GitHealth.Api.Tests.Persistence;

public sealed class DatabaseBackupTests
{
    [Fact]
    public async Task ExportCreatesStandaloneConsistentDatabaseWhileSourceIsOpen()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        await AddProjectAsync(database);
        await using var sourceConnection = database.CreateConnection();
        await sourceConnection.OpenAsync();
        var backupPath = Path.Combine(database.RootPath, "export.db");

        await using (var output = File.Create(backupPath))
        {
            var service = database.Services.GetRequiredService<IDatabaseBackupService>();
            await service.ExportAsync(output, CancellationToken.None);
        }

        Assert.False(File.Exists(backupPath + "-wal"));
        Assert.False(File.Exists(backupPath + "-shm"));
        await using var backup = new SqliteConnection(BuildReadOnlyConnection(backupPath));
        await backup.OpenAsync();
        Assert.Equal(1L, await ReadIntegerAsync(backup, "SELECT COUNT(*) FROM Projects;"));
        Assert.Equal("ok", await ReadTextAsync(backup, "PRAGMA integrity_check;"));
    }

    [Fact]
    public void TemporaryBackupFilesAreOwnerOnlyOnUnix()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        var directory = Path.Combine(
            Path.GetTempPath(),
            "GitHealth-permissions-tests",
            Guid.NewGuid().ToString("N"));
        var path = Path.Combine(directory, "backup.db");
        try
        {
            PrivateFilePermissions.EnsureDirectory(directory);
            PrivateFilePermissions.CreateFile(path);

            Assert.Equal(
                UnixFileMode.UserRead | UnixFileMode.UserWrite,
                File.GetUnixFileMode(path));
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }

    private static async Task AddProjectAsync(SqliteTestDatabase database)
    {
        await using var scope = database.CreateScope();
        var path = Path.Combine(database.RootPath, "repository");
        var project = PersistenceTestData.CreateProject(path);
        var createdAt = new DateTimeOffset(2026, 8, 20, 8, 0, 0, TimeSpan.Zero);
        await scope.ServiceProvider.GetRequiredService<IProjectRepository>()
            .AddAsync(project, createdAt, CancellationToken.None);
    }

    private static string BuildReadOnlyConnection(string path) =>
        new SqliteConnectionStringBuilder
        {
            DataSource = path,
            Mode = SqliteOpenMode.ReadOnly,
            Pooling = false,
        }.ToString();

    private static async Task<long> ReadIntegerAsync(
        SqliteConnection connection,
        string commandText)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = commandText;
        return (long)(await command.ExecuteScalarAsync())!;
    }

    private static async Task<string> ReadTextAsync(
        SqliteConnection connection,
        string commandText)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = commandText;
        return (string)(await command.ExecuteScalarAsync())!;
    }
}
