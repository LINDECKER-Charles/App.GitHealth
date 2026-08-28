using System.Diagnostics;
using App.GitHealth.Api.Persistence.Models;
using App.GitHealth.Api.Persistence.Repositories;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;

namespace App.GitHealth.Api.Tests.Persistence;

public sealed class ConcurrentWriteTests
{
    private static readonly TimeSpan MinimumWait = TimeSpan.FromMilliseconds(800);
    private static readonly TimeSpan MaximumWait = TimeSpan.FromSeconds(5);

    [Fact]
    public async Task ConcurrentWriterRespectsTimeoutAndReturnsControlledError()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        await using var lockConnection = database.CreateConnection();
        await lockConnection.OpenAsync();
        await ExecuteAsync(lockConnection, "BEGIN EXCLUSIVE;");
        var stopwatch = Stopwatch.StartNew();

        try
        {
            var exception = await Assert.ThrowsAsync<PersistenceWriteException>(
                () => AddProjectAsync(database));
            stopwatch.Stop();
            Assert.Equal(PersistenceErrorCode.DatabaseBusy, exception.Code);
            Assert.InRange(stopwatch.Elapsed, MinimumWait, MaximumWait);
        }
        finally
        {
            await ExecuteAsync(lockConnection, "ROLLBACK;");
        }
    }

    private static async Task AddProjectAsync(SqliteTestDatabase database)
    {
        await using var scope = database.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IProjectRepository>();
        var project = PersistenceTestData.CreateProject(Path.Combine(database.RootPath, "locked"));
        var createdAt = new DateTimeOffset(2026, 8, 20, 8, 0, 0, TimeSpan.Zero);
        await repository.AddAsync(project, createdAt, CancellationToken.None);
    }

    private static async Task ExecuteAsync(SqliteConnection connection, string commandText)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = commandText;
        await command.ExecuteNonQueryAsync();
    }
}
