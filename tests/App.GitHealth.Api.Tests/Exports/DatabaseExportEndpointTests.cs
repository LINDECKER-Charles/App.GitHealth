using System.Net;
using App.GitHealth.Api.Tests.Hosting;
using Microsoft.Data.Sqlite;

namespace App.GitHealth.Api.Tests.Exports;

public sealed class DatabaseExportEndpointTests
{
    [Fact]
    public async Task ExportReturnsAStandaloneReadableSqliteDatabase()
    {
        using var factory = new ApiApplicationFactory();
        using var client = factory.CreateClient();
        using var response = await client.GetAsync("/api/exports/database");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/vnd.sqlite3", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal("attachment", response.Content.Headers.ContentDisposition?.DispositionType);
        var bytes = await response.Content.ReadAsByteArrayAsync();
        Assert.NotEmpty(bytes);
        await AssertDatabaseCanBeOpenedAsync(bytes);
    }

    private static async Task AssertDatabaseCanBeOpenedAsync(byte[] bytes)
    {
        var directory = Path.Combine(
            Path.GetTempPath(),
            "GitHealth-export-tests",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var databasePath = Path.Combine(directory, "backup.db");
        try
        {
            await File.WriteAllBytesAsync(databasePath, bytes);
            await AssertSchemaAsync(databasePath);
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            Directory.Delete(directory, recursive: true);
        }
    }

    private static async Task AssertSchemaAsync(string databasePath)
    {
        await using var connection = new SqliteConnection($"Data Source={databasePath}");
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE name = 'Projects';";
        Assert.Equal(1L, await command.ExecuteScalarAsync());
    }
}
