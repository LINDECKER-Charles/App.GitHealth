using App.GitHealth.Api.Persistence.Models;
using App.GitHealth.Api.Persistence.Repositories;
using App.GitHealth.Api.Persistence.Services;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;

namespace App.GitHealth.Api.Tests.Persistence;

public sealed class DatabaseMigrationTests
{
    [Fact]
    public void ExistingParentDirectoryPermissionsArePreserved()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        var directory = Directory.CreateTempSubdirectory("githealth-permissions-").FullName;
        var originalMode = UnixFileMode.UserRead | UnixFileMode.UserWrite
            | UnixFileMode.UserExecute | UnixFileMode.GroupRead
            | UnixFileMode.GroupExecute | UnixFileMode.OtherRead
            | UnixFileMode.OtherExecute;
        try
        {
            File.SetUnixFileMode(directory, originalMode);
            PrivateFilePermissions.EnsureDirectory(directory);

            Assert.Equal(originalMode, File.GetUnixFileMode(directory));
        }
        finally
        {
            Directory.Delete(directory);
        }
    }

    [Fact]
    public async Task InitializeMigratesEmptyAndAlreadyInitializedDatabase()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        var migration = database.Services.GetRequiredService<IDatabaseMigrationService>();

        await migration.InitializeAsync(CancellationToken.None);
        var applied = await CountAppliedMigrationsAsync(database);
        await migration.InitializeAsync(CancellationToken.None);

        await using var connection = database.CreateConnection();
        await connection.OpenAsync();
        // A second initialisation replays nothing: the history stays that of the first run.
        Assert.True(applied > 0);
        Assert.Equal(applied, await ReadIntegerAsync(
            connection,
            "SELECT COUNT(*) FROM __EFMigrationsHistory;"));
        Assert.Equal("wal", await ReadTextAsync(connection, "PRAGMA journal_mode;"));
        Assert.Equal(1L, await ReadIntegerAsync(connection, "PRAGMA foreign_keys;"));
        Assert.True(await ReadIntegerAsync(connection, "PRAGMA busy_timeout;") >= 1000L);
        AssertPrivatePermissions(database.RootPath, database.DatabasePath);
    }

    [Fact]
    public async Task ReopeningCancelsAnalysisInterruptedByPreviousProcess()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        var analysisId = await CreateRunningAnalysisAsync(database);

        await using var reopened = await database.ReopenAsync();
        await using var scope = reopened.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IAnalysisRepository>();
        var analysis = await repository.GetAsync(analysisId, CancellationToken.None);

        Assert.Equal(AnalysisRunStatus.Cancelled, analysis!.Status);
        Assert.Equal("analysis.interrupted", analysis.FailureCode);
        Assert.NotNull(analysis.CompletedAtUtc);
        Assert.Equal(TimeSpan.Zero, analysis.CompletedAtUtc!.Value.Offset);
        Assert.True(analysis.CompletedAtUtc >= analysis.StartedAtUtc);
    }

    private static async Task<Guid> CreateRunningAnalysisAsync(SqliteTestDatabase database)
    {
        await using var scope = database.CreateScope();
        var projects = scope.ServiceProvider.GetRequiredService<IProjectRepository>();
        var analyses = scope.ServiceProvider.GetRequiredService<IAnalysisRepository>();
        var path = Path.Combine(database.RootPath, "repository");
        var project = PersistenceTestData.CreateProject(path);
        var startedAt = DateTimeOffset.UtcNow.AddMinutes(-1);
        await projects.AddAsync(project, startedAt, CancellationToken.None);
        return await analyses.StartAsync(project.Id, startedAt, CancellationToken.None);
    }

    private static async Task<long> CountAppliedMigrationsAsync(SqliteTestDatabase database)
    {
        await using var connection = database.CreateConnection();
        await connection.OpenAsync();
        return await ReadIntegerAsync(connection, "SELECT COUNT(*) FROM __EFMigrationsHistory;");
    }

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

    private static void AssertPrivatePermissions(string directory, string databasePath)
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        var directoryMode = UnixFileMode.UserRead
            | UnixFileMode.UserWrite
            | UnixFileMode.UserExecute;
        var fileMode = UnixFileMode.UserRead | UnixFileMode.UserWrite;
        Assert.Equal(directoryMode, File.GetUnixFileMode(directory));
        foreach (var path in new[] { databasePath, databasePath + "-wal", databasePath + "-shm" })
        {
            if (File.Exists(path))
            {
                Assert.Equal(fileMode, File.GetUnixFileMode(path));
            }
        }
    }
}
