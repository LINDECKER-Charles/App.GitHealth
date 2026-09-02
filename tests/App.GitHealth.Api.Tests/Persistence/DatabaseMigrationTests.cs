using App.GitHealth.Api.Persistence;
using App.GitHealth.Api.Persistence.Models;
using App.GitHealth.Api.Persistence.Repositories;
using App.GitHealth.Api.Persistence.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;

namespace App.GitHealth.Api.Tests.Persistence;

public sealed class DatabaseMigrationTests
{
    /// <summary>Last migration shipped before baselines became a list of their own.</summary>
    private const string BeforeBaselines = "20260829221101_AddProjectOrganization";

    private const string LegacyProjectInsert =
        "INSERT INTO Projects (Id, DisplayName, RepositoryPath, IsRepositoryAccessible, "
        + "CreatedAtUtc, UpdatedAtUtc, ReferenceName, BranchNamespace, ActiveUntilDays, "
        + "InactiveAfterDays, ExcludedPatternsJson, ProtectedPatternsJson, "
        + "LastSuccessfulAnalysisId, IsFavorite) "
        + "VALUES ({0}, 'Legacy', '/legacy/repository', 1, 0, 0, {1}, 'refs/heads/*', "
        + "14, 60, '[]', '[]', {2}, 0);";

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

    /// <summary>
    /// The upgrade an existing installation goes through: a project that only ever knew one
    /// reference must come out of it declaring that reference as its primary baseline, still
    /// pointing at the capture it had.
    /// </summary>
    [Fact]
    public async Task MigrationBackfillsTheExistingReferenceAsPrimaryBaseline()
    {
        var directory = Directory.CreateTempSubdirectory("githealth-backfill-").FullName;
        var databasePath = Path.Combine(directory, "githealth.db");
        var legacy = new LegacyProject(Guid.NewGuid(), "refs/heads/trunk", Guid.NewGuid());
        try
        {
            await MigrateAsync(databasePath, BeforeBaselines);
            await InsertLegacyProjectAsync(databasePath, legacy);
            await MigrateAsync(databasePath, targetMigration: null);

            await AssertBackfilledBaselineAsync(databasePath, legacy);
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            Directory.Delete(directory, true);
        }
    }

    private static async Task AssertBackfilledBaselineAsync(
        string databasePath,
        LegacyProject legacy)
    {
        await using var context = CreateContext(databasePath);
        var baseline = await context.ProjectBaselines.AsNoTracking()
            .SingleAsync(item => item.ProjectId == legacy.Id);
        Assert.Equal(legacy.ReferenceName, baseline.ReferenceName);
        Assert.Equal(0, baseline.Position);
        Assert.Equal(legacy.AnalysisId, baseline.LastSuccessfulAnalysisId);
    }

    private static async Task MigrateAsync(string databasePath, string? targetMigration)
    {
        await using var context = CreateContext(databasePath);
        await context.GetService<IMigrator>().MigrateAsync(targetMigration);
    }

    private static async Task InsertLegacyProjectAsync(
        string databasePath,
        LegacyProject legacy)
    {
        await using var context = CreateContext(databasePath);
        await context.Database.ExecuteSqlRawAsync(
            LegacyProjectInsert,
            legacy.Id,
            legacy.ReferenceName,
            legacy.AnalysisId);
    }

    private static GitHealthDbContext CreateContext(string databasePath)
    {
        var connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = databasePath,
            ForeignKeys = true,
        }.ToString();
        return new GitHealthDbContext(new DbContextOptionsBuilder<GitHealthDbContext>()
            .UseSqlite(connectionString)
            .Options);
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
        return await analyses.StartAsync(
            PersistenceTestData.PrimaryTarget(project.Id),
            startedAt,
            CancellationToken.None);
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

    /// <summary>A row as it existed before ProjectBaselines: one reference, one pointer.</summary>
    private sealed record LegacyProject(Guid Id, string ReferenceName, Guid AnalysisId);
}
