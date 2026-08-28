using App.GitHealth.Api.Persistence.Services;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;

namespace App.GitHealth.Api.Tests.Persistence;

public sealed class DatabaseMigrationTests
{
    [Fact]
    public async Task InitializeMigratesEmptyAndAlreadyInitializedDatabase()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        var migration = database.Services.GetRequiredService<IDatabaseMigrationService>();

        await migration.InitializeAsync(CancellationToken.None);
        await migration.InitializeAsync(CancellationToken.None);

        await using var connection = database.CreateConnection();
        await connection.OpenAsync();
        Assert.Equal(1L, await ReadIntegerAsync(
            connection,
            "SELECT COUNT(*) FROM __EFMigrationsHistory;"));
        Assert.Equal("wal", await ReadTextAsync(connection, "PRAGMA journal_mode;"));
        Assert.Equal(1L, await ReadIntegerAsync(connection, "PRAGMA foreign_keys;"));
        Assert.True(await ReadIntegerAsync(connection, "PRAGMA busy_timeout;") >= 1000L);
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
}
